using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Assistant.Data;

/// <summary>Bitácora del técnico (spec 052, §6). <c>TecnicoId</c> es el <c>sub</c> del token; <c>VenceEn</c> es el cierre de la solicitud más 30 días (null mientras siga abierta).</summary>
internal sealed record BitacoraRecord(string SolicitudId, string TecnicoId, IReadOnlyList<string> Sabe, IReadOnlyList<string> PorConfirmar, IReadOnlyList<string> QueLlevar, DateTime ActualizadaEn, DateTime? VenceEn);

internal interface IBitacoraStore
{
    /// <summary>Crea o reemplaza la bitácora del par solicitud y técnico; verdadero si la creó. El índice único impide duplicados (CA-39).</summary>
    Task<bool> UpsertAsync(BitacoraRecord log, CancellationToken cancellationToken);

    Task<BitacoraRecord?> FindAsync(string solicitudId, string tecnicoId, CancellationToken cancellationToken);
}

/// <summary>Único punto con MongoDB de las bitácoras; todas las consultas filtran por el <c>sub</c> del token (R-23).</summary>
internal sealed class BitacoraStore(IMongoDatabase database) : IBitacoraStore
{
    public const string CollectionName = "bitacoras";

    private IMongoCollection<BsonDocument> Collection => database.GetCollection<BsonDocument>(CollectionName);

    public async Task<bool> UpsertAsync(BitacoraRecord log, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);
        var filter = Filter(log.SolicitudId, log.TecnicoId);
        var u = Builders<BsonDocument>.Update;
        BsonValue expires = log.VenceEn is { } at ? new BsonDateTime(at) : BsonNull.Value;
        var update = u.Combine(
            u.Set("sabe", (BsonValue)new BsonArray(log.Sabe)),
            u.Set("porConfirmar", (BsonValue)new BsonArray(log.PorConfirmar)),
            u.Set("queLlevar", (BsonValue)new BsonArray(log.QueLlevar)),
            u.Set("actualizadaEn", (BsonValue)new BsonDateTime(log.ActualizadaEn)),
            u.Set("venceEn", expires),
            u.SetOnInsert("creadoEn", (BsonValue)new BsonDateTime(log.ActualizadaEn)));
        try
        {
            return await UpsertOnceAsync(filter, update, cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Dos PUT simultáneos: el segundo upsert choca con el índice único y se repite una vez como actualización.
            return await UpsertOnceAsync(filter, update, cancellationToken);
        }
    }

    private async Task<bool> UpsertOnceAsync(FilterDefinition<BsonDocument> filter, UpdateDefinition<BsonDocument> update, CancellationToken cancellationToken) =>
        (await Collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken)).UpsertedId is not null;

    public async Task<BitacoraRecord?> FindAsync(string solicitudId, string tecnicoId, CancellationToken cancellationToken)
    {
        var doc = await Collection.Find(Filter(solicitudId, tecnicoId)).FirstOrDefaultAsync(cancellationToken);
        if (doc is null)
        {
            return null;
        }

        DateTime? expires = doc.TryGetValue("venceEn", out var value) && value.IsValidDateTime ? value.ToUniversalTime() : null;
        return new BitacoraRecord(
            solicitudId,
            tecnicoId,
            [.. doc["sabe"].AsBsonArray.Select(v => v.AsString)],
            [.. doc["porConfirmar"].AsBsonArray.Select(v => v.AsString)],
            [.. doc["queLlevar"].AsBsonArray.Select(v => v.AsString)],
            doc["actualizadaEn"].ToUniversalTime(),
            expires);
    }

    private static FilterDefinition<BsonDocument> Filter(string solicitudId, string tecnicoId) =>
        Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("solicitudId", ObjectId.Parse(solicitudId)), Builders<BsonDocument>.Filter.Eq("tecnicoId", tecnicoId));
}
