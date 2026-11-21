using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Modules.Trust.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Identity.Api.Modules.Trust.Data;

/// <summary>Una verificación en revisión cuyo plazo venció: su id y los tipos de foto que tiene.</summary>
internal sealed record ExpiredVerification(ObjectId Id, IReadOnlyList<string> PhotoTypes);

/// <summary>Lo que el vencimiento horario necesita de MongoDB (spec 025, T-06); existe para probarlo con dobles.</summary>
internal interface IVerificationExpiryStore
{
    /// <summary>Arrendamiento de la tarea en <c>tareas_programadas</c> (spec 003): solo una réplica la corre cada hora. <see langword="true"/> si esta réplica lo obtuvo.</summary>
    Task<bool> TryAcquireLeaseAsync(string taskName, DateTime now, DateTime leaseUntil, CancellationToken cancellationToken);

    /// <summary>Verificaciones <c>en_revision</c> con <c>expiraEn</c> vencido, a lo sumo <paramref name="take"/>.</summary>
    Task<IReadOnlyList<ExpiredVerification>> FindExpiredAsync(DateTime now, int take, CancellationToken cancellationToken);

    /// <summary>
    /// R-16: deja la verificación en <c>sin_verificar</c> con <c>motivo: fotos_borradas</c>, fotos <c>borrada</c>, sin llave envuelta y con <c>curpVigente</c> en falso; el HMAC de la CURP se
    /// conserva. Solo si seguía <c>en_revision</c>: repetirlo no cambia nada (idempotente).
    /// </summary>
    Task<bool> MarkPhotosDeletedAsync(ObjectId id, DateTime now, CancellationToken cancellationToken);
}

/// <summary>Colecciones <c>verificaciones</c> y <c>tareas_programadas</c> para el vencimiento de fotos (spec 025, R-16).</summary>
internal sealed class VerificationExpiryStore(
    [FromKeyedServices(DataExtensions.DatabaseKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> mongoOptions) : IVerificationExpiryStore
{
    public const string ScheduledTasksCollection = "tareas_programadas";
    private static readonly FilterDefinitionBuilder<BsonDocument> _filter = Builders<BsonDocument>.Filter;

    private IMongoCollection<BsonDocument> Verifications => database.GetCollection<BsonDocument>(VerificationStore.VerificationsCollection);

    public async Task<bool> TryAcquireLeaseAsync(string taskName, DateTime now, DateTime leaseUntil, CancellationToken cancellationToken)
    {
        var tasks = database.GetCollection<BsonDocument>(ScheduledTasksCollection);
        var free = _filter.Eq("_id", taskName) & _filter.Lte("arrendadoHasta", now);
        var update = Builders<BsonDocument>.Update.Set("arrendadoHasta", leaseUntil);
        try
        {
            // Primera vez: el upsert crea el documento ya arrendado. Si existe y sigue arrendado, el filtro no casa, el upsert choca con el _id y esta réplica pierde.
            var result = await tasks.UpdateOneAsync(free, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
            return result.ModifiedCount == 1 || result.UpsertedId is not null;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<ExpiredVerification>> FindExpiredAsync(DateTime now, int take, CancellationToken cancellationToken)
    {
        var filter = _filter.Eq("estado", VerificationStates.UnderReview) & _filter.Lte("expiraEn", now);
        var documents = await Verifications.Find(filter, new FindOptions { MaxTime = mongoOptions.Value.QueryMaxTime })
            .Project(Builders<BsonDocument>.Projection.Include("fotos.tipo")).Limit(take).ToListAsync(cancellationToken);
        return
        [
            .. documents.Select(d => new ExpiredVerification(
                d["_id"].AsObjectId,
                d.TryGetValue("fotos", out var photos) && photos.IsBsonArray
                    ? [.. photos.AsBsonArray.Where(p => p.IsBsonDocument && p.AsBsonDocument.TryGetValue("tipo", out var t) && t.IsString).Select(p => p["tipo"].AsString)]
                    : [])),
        ];
    }

    public async Task<bool> MarkPhotosDeletedAsync(ObjectId id, DateTime now, CancellationToken cancellationToken)
    {
        var filter = _filter.Eq("_id", id) & _filter.Eq("estado", VerificationStates.UnderReview);
        var update = Builders<BsonDocument>.Update
            .Set("estado", VerificationStates.NotVerified)
            .Set("motivo", VerificationReasons.PhotosDeleted)
            .Set("curpVigente", false)
            .Set("fotos.$[].estado", PhotoStates.Deleted)
            .Set("fotos.$[].borradoEn", now)
            .Unset("llaveEnvuelta")
            .Unset("llaveKekVersion");
        return (await Verifications.UpdateOneAsync(filter, update, cancellationToken: cancellationToken)).ModifiedCount == 1;
    }
}
