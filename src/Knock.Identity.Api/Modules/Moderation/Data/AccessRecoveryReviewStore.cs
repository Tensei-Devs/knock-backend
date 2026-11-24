using Knock.Hosting.Data;
using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Modules.Moderation.Domain;
using Knock.Identity.Api.Modules.Moderation.Services;
using Knock.Identity.Api.Modules.Panel;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Identity.Api.Modules.Moderation.Data;

/// <summary>
/// La cola y la decisión de las recuperaciones de acceso de la spec 039 dentro del panel de revisión (spec 053, D-07 y CA-36). Las colecciones <c>recuperaciones</c>, <c>usuarios</c> y <c>sesiones</c> son de las
/// specs 039 y 016 (como <c>verificaciones</c> es de la 025): aquí solo se leen los campos de la cola y se escribe la decisión. Aprobar cambia el celular de la cuenta por el que guardó el caso, cierra todas sus
/// sesiones y deja la auditoría en la misma transacción; después de confirmar entrega los ids al puerto <see cref="IRecoverySessionClosures"/> (aviso <c>sesion_revocada</c> y Redis). Rechazar solo cierra el caso. SEGURIDAD: el celular nuevo
/// pasa de un documento a otro ya protegido (cifrado y HMAC); nunca se descifra ni se registra. La cola solo trae identificador, tipo, estado y fecha.
/// </summary>
internal sealed class AccessRecoveryReviewStore(
    [FromKeyedServices(DataExtensions.DatabaseKey)] IMongoDatabase database,
    IRecoverySessionClosures closures,
    TimeProvider clock,
    IOptions<KnockMongoOptions> mongoOptions) : IAccessRecoveryReviews
{
    private const string _recoveries = "recuperaciones";
    private const string _users = "usuarios";
    private const string _sessions = "sesiones";
    private const string _typeField = "tipo";
    private const string _newPhoneField = "celularNuevoProtegido";
    private const string _pending = "pendiente";
    private const string _approved = "aprobada";
    private const string _rejected = "rechazada";
    private const string _clientType = "cliente";

    private const string _idField = "_id";
    private const string _stateField = "estado";
    private const string _userField = "usuarioId";
    private const string _createdField = "creadoEn";
    private const string _updatedField = "actualizadoEn";
    private const string _decidedByField = "decididoPor";
    private const string _decidedAtField = "decididoEn";
    private const string _revokedField = "revocada";
    private const string _expiresField = "venceEn";

    /// <summary>Lo que dura un token de acceso (spec 016); pasado ese tiempo desde la decisión ya no puede quedar uno vivo.</summary>
    private static readonly TimeSpan _accessTokenLifetime = TimeSpan.FromMinutes(15);

    private static readonly FilterDefinitionBuilder<BsonDocument> _filter = Builders<BsonDocument>.Filter;

    private IMongoCollection<BsonDocument> Recoveries => database.GetCollection<BsonDocument>(_recoveries);

    private IMongoCollection<BsonDocument> Users => database.GetCollection<BsonDocument>(_users);

    private IMongoCollection<BsonDocument> Sessions => database.GetCollection<BsonDocument>(_sessions);

    /// <summary>Los estados de la cola de verificaciones que corresponden a los de la recuperación (§6 de la spec 039).</summary>
    private static string? RecoveryState(string queueState) => queueState switch
    {
        ReviewStates.UnderReview => _pending,
        ReviewStates.Verified => _approved,
        ReviewStates.Rejected => _rejected,
        _ => null,
    };

    public async Task<IReadOnlyList<QueuedVerification>> ListAsync(string state, DateTime from, DateTime to, ReviewCursor? after, int take, CancellationToken cancellationToken)
    {
        if (RecoveryState(state) is not { } recoveryState)
        {
            return [];
        }

        var filter = _filter.Eq(_stateField, recoveryState) & _filter.Gte(_createdField, from) & _filter.Lte(_createdField, to);
        if (after is { } cursor)
        {
            filter &= _filter.Gt(_createdField, cursor.At) | (_filter.Eq(_createdField, cursor.At) & _filter.Gt(_idField, cursor.Id));
        }

        var documents = await Recoveries.Find(filter, new FindOptions { MaxTime = mongoOptions.Value.QueryMaxTime })
            .Sort(Builders<BsonDocument>.Sort.Ascending(_createdField).Ascending(_idField))
            .Project(Builders<BsonDocument>.Projection.Include(_typeField).Include(_createdField))
            .Limit(take).ToListAsync(cancellationToken);

        // El campo «nivel» de la cola lleva aquí el tipo de cuenta que dice tener la persona (tecnico o cliente).
        return [.. documents.Select(d => new QueuedVerification(d[_idField].AsObjectId, d.TryGetValue(_typeField, out var type) && type.IsString ? type.AsString : _clientType, state, d[_createdField].ToUniversalTime()))];
    }

    public Task<RecoveryDecisionOutcome> DecideAsync(ObjectId id, bool approved, string deciderSub, Func<StoreTransaction, Task> writeAudit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deciderSub);
        ArgumentNullException.ThrowIfNull(writeAudit);
        return DecisionTurns.RunAsync(
            id.ToString(),
            () => SettledAsync(id, approved, cancellationToken),
            Map,
            () => DecideAndNotifyAsync(id, approved, deciderSub, writeAudit, cancellationToken),
            cancellationToken);
    }

    private static RecoveryDecisionOutcome Map(DecisionStoreOutcome outcome) =>
        outcome == DecisionStoreOutcome.NotFound ? RecoveryDecisionOutcome.NotFound : RecoveryDecisionOutcome.AlreadyDecided;

    private async Task<DecisionStoreOutcome?> SettledAsync(ObjectId id, bool approved, CancellationToken cancellationToken)
    {
        var current = await Recoveries.Find(_filter.Eq(_idField, id), new FindOptions { MaxTime = mongoOptions.Value.QueryMaxTime })
            .Project(Builders<BsonDocument>.Projection.Include(_stateField).Include(_userField).Include(_decidedAtField)).Limit(1).FirstOrDefaultAsync(cancellationToken);
        if (current is null)
        {
            return DecisionStoreOutcome.NotFound;
        }

        if (current.TryGetValue(_stateField, out var state) && state == _pending)
        {
            return null;
        }

        if (approved && state == _approved)
        {
            await RetryCancellationAsync(current, cancellationToken);
        }

        return DecisionStoreOutcome.AlreadyDecided;
    }

    /// <summary>
    /// Si la aprobación confirmó pero Redis falló al cancelar las sesiones, la petición respondió error y quien reintenta recibe 409; antes de responderlo se repite la cancelación (idempotente) de las sesiones revocadas
    /// de esa cuenta que aún no vencen, mientras pueda quedar un token de acceso vivo (15 minutos desde la decisión). Así una sesión no queda viva tras cambiar el celular.
    /// </summary>
    private async Task RetryCancellationAsync(BsonDocument recovery, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (!recovery.TryGetValue(_decidedAtField, out var decidedAt) || !decidedAt.IsValidDateTime || now - decidedAt.ToUniversalTime() > _accessTokenLifetime
            || !recovery.TryGetValue(_userField, out var userId) || !userId.IsObjectId)
        {
            return;
        }

        var closed = _filter.Eq(_userField, userId.AsObjectId) & _filter.Eq(_revokedField, true) & _filter.Gt(_expiresField, new BsonDateTime(now));
        var ids = (await Sessions.Find(closed, new FindOptions { MaxTime = mongoOptions.Value.QueryMaxTime }).Project(Builders<BsonDocument>.Projection.Include(_idField)).ToListAsync(cancellationToken))
            .Select(document => document[_idField].AsObjectId.ToString()).ToList();
        if (ids.Count > 0)
        {
            await closures.RetryCancellationAsync(ids, cancellationToken);
        }
    }

    /// <summary>D-05: quien decide no puede ser la persona de la recuperación (el <c>sub</c> de la cuenta del caso).</summary>
    private async Task<bool> IsOwnRecoveryAsync(ObjectId id, string deciderSub, CancellationToken cancellationToken)
    {
        var recovery = await Recoveries.Find(_filter.Eq(_idField, id), new FindOptions { MaxTime = mongoOptions.Value.QueryMaxTime })
            .Project(Builders<BsonDocument>.Projection.Include(_userField)).Limit(1).FirstOrDefaultAsync(cancellationToken);
        if (recovery is null || !recovery.TryGetValue(_userField, out var userId) || !userId.IsObjectId)
        {
            return false;
        }

        var owner = await Users.Find(_filter.Eq(_idField, userId.AsObjectId), new FindOptions { MaxTime = mongoOptions.Value.QueryMaxTime })
            .Project(Builders<BsonDocument>.Projection.Include("sub")).Limit(1).FirstOrDefaultAsync(cancellationToken);
        return owner is not null && owner.TryGetValue("sub", out var sub) && sub.IsString && string.Equals(sub.AsString, deciderSub, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<RecoveryDecisionOutcome> DecideAndNotifyAsync(ObjectId id, bool approved, string deciderSub, Func<StoreTransaction, Task> writeAudit, CancellationToken cancellationToken)
    {
        if (await IsOwnRecoveryAsync(id, deciderSub, cancellationToken))
        {
            return RecoveryDecisionOutcome.SelfReview;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        IReadOnlyList<string> revoked;
        try
        {
            using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
            var result = await session.WithTransactionAsync(
                (transaction, token) => DecideInTransactionAsync(transaction, id, approved, deciderSub, now, writeAudit, token),
                cancellationToken: cancellationToken);
            if (result.Outcome != RecoveryDecisionOutcome.Decided)
            {
                return result.Outcome;
            }

            revoked = result.RevokedSessionIds;
        }
        catch (Exception exception) when (exception is MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey } or MongoCommandException { Code: 11000 })
        {
            return RecoveryDecisionOutcome.PhoneTaken;
        }

        // Después de confirmar: el aviso a los dispositivos y la lista de sesiones canceladas que lee el gateway (spec 016).
        await closures.AfterCommitAsync(revoked, cancellationToken);

        return RecoveryDecisionOutcome.Decided;
    }

    private async Task<(RecoveryDecisionOutcome Outcome, IReadOnlyList<string> RevokedSessionIds)> DecideInTransactionAsync(
        IClientSessionHandle transaction, ObjectId id, bool approved, string deciderSub, DateTime now, Func<StoreTransaction, Task> writeAudit, CancellationToken cancellationToken)
    {
        var update = Builders<BsonDocument>.Update
            .Set(_stateField, approved ? _approved : _rejected)
            .Set(_decidedByField, deciderSub)
            .Set(_decidedAtField, new BsonDateTime(now))
            .Set(_updatedField, new BsonDateTime(now));
        var recovery = await Recoveries.FindOneAndUpdateAsync(
            transaction,
            _filter.Eq(_idField, id) & _filter.Eq(_stateField, _pending),
            update,
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (recovery is null)
        {
            var exists = await Recoveries.Find(transaction, _filter.Eq(_idField, id)).Limit(1).AnyAsync(cancellationToken);
            return (exists ? RecoveryDecisionOutcome.AlreadyDecided : RecoveryDecisionOutcome.NotFound, []);
        }

        IReadOnlyList<string> revoked = [];
        if (approved)
        {
            var applied = await ApplyNewPhoneAsync(transaction, recovery, now, cancellationToken);
            if (applied is null)
            {
                await transaction.AbortTransactionAsync(cancellationToken);
                return (RecoveryDecisionOutcome.NotFound, []);
            }

            revoked = applied;
        }

        await writeAudit(new StoreTransaction(transaction));
        return (RecoveryDecisionOutcome.Decided, revoked);
    }

    /// <summary>Cambia el celular de la cuenta por el protegido del caso y revoca todas sus sesiones abiertas. <see langword="null"/> si la cuenta ya no existe.</summary>
    private async Task<IReadOnlyList<string>?> ApplyNewPhoneAsync(IClientSessionHandle transaction, BsonDocument recovery, DateTime now, CancellationToken cancellationToken)
    {
        var userId = recovery[_userField].AsObjectId;
        var phone = recovery[_newPhoneField].AsBsonDocument;
        var userUpdate = Builders<BsonDocument>.Update
            .Set("celularCifrado", phone["cifrado"])
            .Set("celularCifradoVersion", phone["cifradoVersion"])
            .Set("celularHash", phone["hash"])
            .Set("celularHashVersion", phone["hashVersion"])
            .Set(_updatedField, new BsonDateTime(now))
            .Inc("version", 1);
        var changed = await Users.UpdateOneAsync(transaction, _filter.Eq(_idField, userId), userUpdate, cancellationToken: cancellationToken);
        if (changed.MatchedCount == 0)
        {
            return null;
        }

        var open = _filter.Eq(_userField, userId) & _filter.Eq(_revokedField, false) & _filter.Gt(_expiresField, new BsonDateTime(now));
        var ids = (await Sessions.Find(transaction, open).Project(Builders<BsonDocument>.Projection.Include(_idField)).ToListAsync(cancellationToken))
            .Select(document => document[_idField].AsObjectId)
            .ToList();
        if (ids.Count > 0)
        {
            await Sessions.UpdateManyAsync(transaction, _filter.In(_idField, ids), Builders<BsonDocument>.Update.Set(_revokedField, true), cancellationToken: cancellationToken);
        }

        return [.. ids.Select(sessionId => sessionId.ToString())];
    }
}
