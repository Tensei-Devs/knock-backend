using Knock.Hosting.Concurrency;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Assistant.Data;

/// <summary>Bitácora del técnico (spec 052, §6). <c>TecnicoId</c> es el <c>sub</c> del token; <c>VenceEn</c> es el cierre de la solicitud más 30 días (null mientras siga abierta).</summary>
internal sealed record BitacoraRecord(string SolicitudId, string TecnicoId, IReadOnlyList<string> Sabe, IReadOnlyList<string> PorConfirmar, IReadOnlyList<string> QueLlevar, DateTime ActualizadaEn, DateTime? VenceEn);

/// <summary>Una bitácora que todavía no tiene fecha de borrado. <paramref name="Id"/> sirve de cursor para recorrerlas.</summary>
internal sealed record BitacoraSinFecha(string Id, string SolicitudId);

internal interface IBitacoraStore
{
    /// <summary>Crea o reemplaza la bitácora del par solicitud y técnico; verdadero si la creó. El índice único impide duplicados (CA-39).</summary>
    Task<bool> UpsertAsync(BitacoraRecord log, CancellationToken cancellationToken);

    Task<BitacoraRecord?> FindAsync(string solicitudId, string tecnicoId, CancellationToken cancellationToken);

    /// <summary>Hasta <paramref name="limit"/> bitácoras sin <c>venceEn</c>, en orden, después de <paramref name="afterId"/>.</summary>
    Task<IReadOnlyList<BitacoraSinFecha>> ListWithoutExpiryAsync(string? afterId, int limit, CancellationToken cancellationToken);

    /// <summary>Pone <c>venceEn</c> a las bitácoras de la solicitud que no lo tengan; repetirlo no cambia nada.</summary>
    Task SetExpiryAsync(string solicitudId, DateTime venceEn, CancellationToken cancellationToken);
}

/// <summary>Único punto con MongoDB de las bitácoras; todas las consultas filtran por el <c>sub</c> del token (R-23).</summary>
internal sealed class BitacoraStore(IMongoDatabase database) : IBitacoraStore
{
    public const string CollectionName = "bitacoras";

    private IMongoCollection<BsonDocument> Collection => database.GetCollection<BsonDocument>(CollectionName);

    /// <summary>Dentro de la instancia, los PUT del mismo par solicitud y técnico se turnan (<see cref="KeyedGate"/>): varios upsert a la vez sobre el índice único se bloquean entre sí en documentdb-local.</summary>
    public Task<bool> UpsertAsync(BitacoraRecord log, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);
        return KeyedGate.RunAsync(log.SolicitudId + "|" + log.TecnicoId, () => UpsertSerializedAsync(log, cancellationToken), cancellationToken);
    }

    private async Task<bool> UpsertSerializedAsync(BitacoraRecord log, CancellationToken cancellationToken)
    {
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

    public async Task<IReadOnlyList<BitacoraSinFecha>> ListWithoutExpiryAsync(string? afterId, int limit, CancellationToken cancellationToken)
    {
        var f = Builders<BsonDocument>.Filter;
        var filter = f.Eq("venceEn", BsonNull.Value);
        if (afterId is not null)
        {
            filter &= f.Gt("_id", ObjectId.Parse(afterId));
        }

        var found = await Collection.Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Ascending("_id"))
            .Limit(limit)
            .Project(Builders<BsonDocument>.Projection.Include("solicitudId"))
            .ToListAsync(cancellationToken);
        return [.. found.Select(d => new BitacoraSinFecha(d["_id"].AsObjectId.ToString(), d["solicitudId"].AsObjectId.ToString()))];
    }

    public Task SetExpiryAsync(string solicitudId, DateTime venceEn, CancellationToken cancellationToken)
    {
        var f = Builders<BsonDocument>.Filter;
        return Collection.UpdateManyAsync(
            f.Eq("solicitudId", ObjectId.Parse(solicitudId)) & f.Eq("venceEn", BsonNull.Value),
            Builders<BsonDocument>.Update.Set("venceEn", venceEn),
            cancellationToken: cancellationToken);
    }

    private static FilterDefinition<BsonDocument> Filter(string solicitudId, string tecnicoId) =>
        Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("solicitudId", ObjectId.Parse(solicitudId)), Builders<BsonDocument>.Filter.Eq("tecnicoId", tecnicoId));
}
