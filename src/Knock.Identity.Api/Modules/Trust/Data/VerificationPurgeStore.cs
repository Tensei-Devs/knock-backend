using System.Globalization;
using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Hosting.Procesos.Outbox;
using Knock.Identity.Api.Modules.Panel;
using Knock.Identity.Api.Modules.Trust.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Identity.Api.Modules.Trust.Data;

/// <summary>Cómo terminó una purga (spec 057, R-06: repetirla no cambia nada).</summary>
internal enum PurgeOutcome
{
    Purged,
    AlreadyPurged,
    NotEligible,
    NotFound,
}

/// <summary>Lo que la purga necesita de <c>verificaciones</c>; existe para probar el servicio y los manejadores con dobles sin base de datos.</summary>
internal interface IVerificationPurgeStore
{
    /// <summary>
    /// En una transacción: destruye la llave envuelta, deja las fotos como <c>borrada</c>, fija el estado final, escribe el evento <c>FotosDeVerificacionBorradas</c> y ejecuta <paramref name="writeAudit"/>.
    /// La transición está condicionada por <c>purgadaEn</c> y por el estado, así que gana la primera escritura (R-07).
    /// </summary>
    Task<PurgeOutcome> PurgeAsync(ObjectId id, string reason, DateTime now, Func<StoreTransaction, Task> writeAudit, CancellationToken cancellationToken);

    /// <summary>Marca <c>alertadaEn</c> y escribe <c>VerificacionSinRevisarAlertada</c> una sola vez; <see langword="false"/> si ya se decidió, se purgó o se alertó antes.</summary>
    Task<bool> MarkAlertedAsync(ObjectId id, DateTime now, CancellationToken cancellationToken);

    /// <summary>Las verificaciones de la persona que aún conservan fotos o llave (baja de cuenta).</summary>
    Task<IReadOnlyList<ObjectId>> FindUnpurgedAsync(string sub, CancellationToken cancellationToken);
}

/// <summary>
/// Escrituras de la purga sobre <c>verificaciones</c> (spec 057, §5). Es el único almacén, junto con la decisión del panel (spec 053), que destruye la llave envuelta. Nunca lee ni devuelve
/// CURP, HMAC, rutas de blob ni la llave.
/// </summary>
internal sealed class VerificationPurgeStore(
    [FromKeyedServices(DataExtensions.DatabaseKey)] IMongoDatabase database,
    IOutboxStore outbox) : IVerificationPurgeStore
{
    private const string _idField = "_id";
    private const string _stateField = "estado";
    private const string _purgedField = "purgadaEn";
    private const string _photosField = "fotos";
    private const string _ownerField = "usuarioId";

    private static readonly FilterDefinitionBuilder<BsonDocument> _filter = Builders<BsonDocument>.Filter;

    private IMongoCollection<BsonDocument> Verifications => database.GetCollection<BsonDocument>(VerificationStore.VerificationsCollection);

    public async Task<PurgeOutcome> PurgeAsync(ObjectId id, string reason, DateTime now, Func<StoreTransaction, Task> writeAudit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writeAudit);
        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        return await session.WithTransactionAsync(
            async (transaction, token) =>
            {
                var update = Builders<BsonDocument>.Update
                    .Set(_stateField, VerificationStates.NotVerified)
                    .Set("curpVigente", false)
                    .Set(_purgedField, now)
                    .Set("razonPurga", reason)
                    .Set("llaveEnvuelta", BsonNull.Value)
                    .Set("llaveKekVersion", BsonNull.Value)
                    .Set($"{_photosField}.$[].estado", PhotoStates.Deleted)
                    .Set($"{_photosField}.$[].blob", BsonNull.Value)
                    .Set($"{_photosField}.$[].borradoEn", now);
                update = reason == PurgeReasons.Expired ? update.Set("motivo", VerificationReasons.PhotosDeleted) : update;
                var matched = await Verifications.FindOneAndUpdateAsync(
                    transaction,
                    EligibleFilter(id, reason),
                    update,
                    new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After, Projection = Builders<BsonDocument>.Projection.Include(_ownerField) },
                    token);
                if (matched is null)
                {
                    return await ExplainAsync(transaction, id, token);
                }

                var owner = matched.TryGetValue(_ownerField, out var sub) && sub.IsString ? sub.AsString : string.Empty;
                await outbox.AddAsync(
                    transaction,
                    new EventoDeOutbox(
                        OutboxReglas.BuildId(PurgeEvents.PhotosDeleted, id.ToString(), 1),
                        PurgeEvents.PhotosDeleted,
                        "messaging",
                        new BsonDocument { ["verificacionId"] = id.ToString(), ["usuarioId"] = owner, ["razon"] = reason }),
                    now,
                    token);
                await writeAudit(new StoreTransaction(transaction));
                return PurgeOutcome.Purged;
            },
            cancellationToken: cancellationToken);
    }

    public async Task<bool> MarkAlertedAsync(ObjectId id, DateTime now, CancellationToken cancellationToken)
    {
        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        return await session.WithTransactionAsync(
            async (transaction, token) =>
            {
                var filter = _filter.Eq(_idField, id) & _filter.Eq(_stateField, VerificationStates.UnderReview) & _filter.Eq("alertadaEn", BsonNull.Value) & _filter.Eq(_purgedField, BsonNull.Value);
                var matched = await Verifications.FindOneAndUpdateAsync(
                    transaction,
                    filter,
                    Builders<BsonDocument>.Update.Set("alertadaEn", now),
                    new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After, Projection = Builders<BsonDocument>.Projection.Include("enviadoEn") },
                    token);
                if (matched is null)
                {
                    return false;
                }

                var sent = matched.TryGetValue("enviadoEn", out var value) && value.IsValidDateTime ? value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) : string.Empty;
                await outbox.AddAsync(
                    transaction,
                    new EventoDeOutbox(
                        OutboxReglas.BuildId(PurgeEvents.UnreviewedAlert, id.ToString(), 1),
                        PurgeEvents.UnreviewedAlert,
                        "identity",
                        new BsonDocument { ["verificacionId"] = id.ToString(), ["enviadoEn"] = sent }),
                    now,
                    token);
                return true;
            },
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ObjectId>> FindUnpurgedAsync(string sub, CancellationToken cancellationToken)
    {
        var documents = await Verifications.Find(_filter.Eq(_ownerField, sub) & _filter.Eq(_purgedField, BsonNull.Value))
            .Project(Builders<BsonDocument>.Projection.Include(_idField)).ToListAsync(cancellationToken);
        return [.. documents.Select(d => d[_idField].AsObjectId)];
    }

    /// <summary>Vencimiento: solo si sigue en revisión. Baja de cuenta: en cualquier estado. En ambos casos solo si aún no se purgó (el campo vale null o no existe).</summary>
    private static FilterDefinition<BsonDocument> EligibleFilter(ObjectId id, string reason)
    {
        var filter = _filter.Eq(_idField, id) & _filter.Eq(_purgedField, BsonNull.Value);
        return reason == PurgeReasons.AccountDeletion ? filter : filter & _filter.Eq(_stateField, VerificationStates.UnderReview);
    }

    private async Task<PurgeOutcome> ExplainAsync(IClientSessionHandle transaction, ObjectId id, CancellationToken token)
    {
        var current = await Verifications.Find(transaction, _filter.Eq(_idField, id)).Project(Builders<BsonDocument>.Projection.Include(_purgedField)).Limit(1).FirstOrDefaultAsync(token);
        if (current is null)
        {
            return PurgeOutcome.NotFound;
        }

        return current.TryGetValue(_purgedField, out var purged) && !purged.IsBsonNull ? PurgeOutcome.AlreadyPurged : PurgeOutcome.NotEligible;
    }
}
