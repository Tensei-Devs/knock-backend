using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Modules.Moderation.Domain;
using Knock.Identity.Api.Modules.Panel.Data;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Identity.Api.Modules.Moderation.Data;

/// <summary>Una fila de la cola de verificaciones: sin fotos ni datos personales (spec 053, R-16).</summary>
internal sealed record QueuedVerification(ObjectId Id, string Level, string State, DateTime SentAt);

/// <summary>Lo que el panel necesita de una verificación para abrirla o decidirla. <see cref="OwnerSub"/> solo sirve para negar que la persona verificada se revise a sí misma (D-05).</summary>
internal sealed record ReviewedVerification(ObjectId Id, string OwnerSub, string Level, string State, DateTime SentAt, IReadOnlyList<string> PhotoTypes, bool PhotosDestroyed);

/// <summary>La decisión que se guarda (R-18). <c>Note</c> es el texto del verificador; <see cref="CatalogReason"/> solo viene si coincide con un motivo del catálogo de la spec 025.</summary>
internal sealed record VerificationDecisionRecord(ObjectId Id, bool Approved, string DeciderSub, string Note, string? CatalogReason, DateTime DecidedAt);

/// <summary>Cómo terminó guardar una decisión.</summary>
internal enum DecisionStoreOutcome
{
    Decided,
    NotFound,
    AlreadyDecided,
    CurpDuplicate,
}

/// <summary>Lo que el panel necesita de <c>verificaciones</c>; existe para probar los servicios con dobles sin base de datos.</summary>
internal interface IVerificationReviewStore
{
    /// <summary>La cola de <paramref name="state"/> entre <paramref name="from"/> y <paramref name="to"/> (por <c>enviadoEn</c>), de la más antigua a la más reciente; pide <paramref name="take"/> filas.</summary>
    Task<IReadOnlyList<QueuedVerification>> ListAsync(string state, DateTime from, DateTime to, ReviewCursor? after, int take, CancellationToken cancellationToken);

    Task<ReviewedVerification?> FindAsync(ObjectId id, CancellationToken cancellationToken);

    /// <summary>
    /// En una sola transacción: pasa la verificación de <c>en_revision</c> a su estado final, destruye la llave envuelta y deja las fotos como <c>borrada</c> (la purga irreversible de la spec 057,
    /// D-02) y ejecuta <paramref name="writeAudit"/>; si la auditoría falla no queda nada (R-22).
    /// </summary>
    Task<DecisionStoreOutcome> DecideAsync(VerificationDecisionRecord decision, Func<StoreTransaction, Task> writeAudit, CancellationToken cancellationToken);
}

/// <summary>
/// Lectura y decisión sobre <c>verificaciones</c> para el panel (spec 053, VER-03 a VER-05). La colección es de Trust (spec 025); aquí solo se leen los campos de la cola y se escribe la decisión.
/// Nunca devuelve CURP, su HMAC, la llave envuelta ni rutas de blob.
/// </summary>
internal sealed class VerificationReviewStore(
    [FromKeyedServices(DataExtensions.DatabaseKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> mongoOptions) : IVerificationReviewStore
{
    public const string Collection = "verificaciones";
    private const string _idField = "_id";
    private const string _stateField = "estado";
    private const string _sentField = "enviadoEn";
    private const string _photosField = "fotos";

    private static readonly FilterDefinitionBuilder<BsonDocument> _filter = Builders<BsonDocument>.Filter;

    private IMongoCollection<BsonDocument> Verifications => database.GetCollection<BsonDocument>(Collection);

    private FindOptions FindOptions => new() { MaxTime = mongoOptions.Value.QueryMaxTime };

    /// <summary>Índice de la cola (§6): estado y antigüedad. Idempotente; coincide con <c>db/053-panel-de-revision.js</c>.</summary>
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var queue = Builders<BsonDocument>.IndexKeys.Ascending(_stateField).Ascending(_sentField);
        await Verifications.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(queue, new CreateIndexOptions { Name = "estado_1_enviadoEn_1" }), cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<QueuedVerification>> ListAsync(string state, DateTime from, DateTime to, ReviewCursor? after, int take, CancellationToken cancellationToken)
    {
        var filter = _filter.Eq(_stateField, state) & _filter.Gte(_sentField, from) & _filter.Lte(_sentField, to);
        if (after is { } cursor)
        {
            filter &= _filter.Gt(_sentField, cursor.At) | (_filter.Eq(_sentField, cursor.At) & _filter.Gt(_idField, cursor.Id));
        }

        var documents = await Verifications.Find(filter, FindOptions)
            .Sort(Builders<BsonDocument>.Sort.Ascending(_sentField).Ascending(_idField))
            .Project(Builders<BsonDocument>.Projection.Include("nivel").Include(_stateField).Include(_sentField))
            .Limit(take).ToListAsync(cancellationToken);
        return [.. documents.Select(d => new QueuedVerification(d[_idField].AsObjectId, Text(d, "nivel") ?? "basica", Text(d, _stateField) ?? state, d[_sentField].ToUniversalTime()))];
    }

    public async Task<ReviewedVerification?> FindAsync(ObjectId id, CancellationToken cancellationToken)
    {
        var d = await Verifications.Find(_filter.Eq(_idField, id), FindOptions)
            .Project(Builders<BsonDocument>.Projection.Include("usuarioId").Include("nivel").Include(_stateField).Include(_sentField).Include(_photosField).Include("purgadaEn"))
            .Limit(1).FirstOrDefaultAsync(cancellationToken);
        if (d is null)
        {
            return null;
        }

        var photos = d.TryGetValue(_photosField, out var array) && array.IsBsonArray ? array.AsBsonArray.Where(p => p.IsBsonDocument).Select(p => p.AsBsonDocument).ToList() : [];
        var destroyed = (d.TryGetValue("purgadaEn", out var purged) && !purged.IsBsonNull) || (photos.Count > 0 && photos.All(p => Text(p, _stateField) == "borrada"));
        return new ReviewedVerification(
            d[_idField].AsObjectId,
            Text(d, "usuarioId") ?? string.Empty,
            Text(d, "nivel") ?? "basica",
            Text(d, _stateField) ?? string.Empty,
            d.TryGetValue(_sentField, out var sent) && sent.IsValidDateTime ? sent.ToUniversalTime() : DateTime.MinValue,
            [.. photos.Select(p => Text(p, "tipo") ?? string.Empty).Where(t => t.Length > 0)],
            destroyed);
    }

    public async Task<DecisionStoreOutcome> DecideAsync(VerificationDecisionRecord decision, Func<StoreTransaction, Task> writeAudit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(writeAudit);
        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        try
        {
            return await session.WithTransactionAsync(
                async (transaction, token) =>
                {
                    var update = Builders<BsonDocument>.Update
                        .Set(_stateField, decision.Approved ? ReviewStates.Verified : ReviewStates.Rejected)
                        .Set("decididoEn", decision.DecidedAt)
                        .Set("decididoPor", decision.DeciderSub)
                        .Set("notaDeRevision", decision.Note)
                        .Set("purgadaEn", decision.DecidedAt)
                        .Set("razonPurga", "decidida")
                        .Set("llaveEnvuelta", BsonNull.Value)
                        .Set("llaveKekVersion", BsonNull.Value)
                        .Set($"{_photosField}.$[].estado", "borrada")
                        .Set($"{_photosField}.$[].blob", BsonNull.Value)
                        .Set($"{_photosField}.$[].borradoEn", decision.DecidedAt);
                    update = decision.CatalogReason is null ? update.Unset("motivo") : update.Set("motivo", decision.CatalogReason);
                    var matched = await Verifications.FindOneAndUpdateAsync(
                        transaction,
                        _filter.Eq(_idField, decision.Id) & _filter.Eq(_stateField, ReviewStates.UnderReview),
                        update,
                        new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After, Projection = Builders<BsonDocument>.Projection.Include("nivel").Include("curpHmac") },
                        token);
                    if (matched is null)
                    {
                        var exists = await Verifications.Find(transaction, _filter.Eq(_idField, decision.Id)).Limit(1).AnyAsync(token);
                        return exists ? DecisionStoreOutcome.AlreadyDecided : DecisionStoreOutcome.NotFound;
                    }

                    // Aprobar otorga el nivel pedido y, si hay HMAC de la CURP (spec 025, T-03), la vuelve vigente: el índice único parcial impide dos cuentas con la misma CURP.
                    var grant = decision.Approved
                        ? Builders<BsonDocument>.Update.Set("nivelOtorgado", Text(matched, "nivel") ?? "basica").Set("curpVigente", matched.TryGetValue("curpHmac", out var hmac) && !hmac.IsBsonNull)
                        : Builders<BsonDocument>.Update.Set("curpVigente", false);
                    await Verifications.UpdateOneAsync(transaction, _filter.Eq(_idField, decision.Id), grant, cancellationToken: token);
                    await writeAudit(new StoreTransaction(transaction));
                    return DecisionStoreOutcome.Decided;
                },
                cancellationToken: cancellationToken);
        }
        catch (MongoCommandException exception) when (exception.Code == 11000)
        {
            return DecisionStoreOutcome.CurpDuplicate;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return DecisionStoreOutcome.CurpDuplicate;
        }
    }

    private static string? Text(BsonDocument d, string name) => d.TryGetValue(name, out var v) && v.IsString ? v.AsString : null;
}
