using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Requests.Data;

/// <summary>Lo que Mercado necesita de la proyección de bloqueos (spec 043, R-16): existe para probar los servicios con dobles.</summary>
internal interface IBlockLookup
{
    /// <summary>Si alguna de las dos personas bloqueó a la otra (el bloqueo corta en ambos sentidos).</summary>
    Task<bool> AreBlockedAsync(string personA, string personB, CancellationToken token);

    /// <summary>De <paramref name="others"/>, las personas con un bloqueo en cualquier sentido con <paramref name="person"/>, en una sola consulta.</summary>
    Task<IReadOnlySet<string>> BlockedWithAsync(string person, IEnumerable<string> others, CancellationToken token);
}

/// <summary>
/// Proyección mínima <c>bloqueos</c> de Mercado (spec 043, R-23): solo los dos identificadores públicos, el estado y la fecha del último evento. El <c>_id</c> es el par («bloquea|bloqueado»),
/// así que no necesita más índices. Nunca lee la base de Identidad.
/// </summary>
internal sealed class BlockProjectionStore([FromKeyedServices(RequestsModuleExtensions.ModuleKey)] IMongoDatabase database) : IBlockLookup
{
    public const string CollectionName = "bloqueos";
    private const string _blockedField = "bloqueado";
    private const string _atField = "ocurridoEn";

    private IMongoCollection<BsonDocument> Blocks => database.GetCollection<BsonDocument>(CollectionName);

    /// <summary>Aplica <c>BloqueoCreado</c> (<paramref name="blocked"/> verdadero) o <c>BloqueoEliminado</c>. Idempotente y a prueba de desorden: un evento más viejo que el aplicado no cambia nada.</summary>
    public async Task ApplyAsync(string blockerId, string blockedId, bool blocked, DateTime occurredAtUtc, string eventId, CancellationToken token)
    {
        var key = $"{blockerId}|{blockedId}";
        var f = Builders<BsonDocument>.Filter;
        var document = new BsonDocument
        {
            ["_id"] = key,
            ["bloqueaId"] = blockerId,
            ["bloqueadoId"] = blockedId,
            [_blockedField] = blocked,
            [_atField] = new BsonDateTime(occurredAtUtc),
            ["eventoId"] = eventId,
        };
        try
        {
            await Blocks.ReplaceOneAsync(f.Eq("_id", key) & f.Lte(_atField, new BsonDateTime(occurredAtUtc)), document, new ReplaceOptions { IsUpsert = true }, token);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Ya hay un evento más reciente de este par: el viejo (o el repetido) no cambia nada.
        }
    }

    /// <summary>Si alguna de las dos personas bloqueó a la otra (el bloqueo corta en ambos sentidos, R-16).</summary>
    public async Task<bool> AreBlockedAsync(string personA, string personB, CancellationToken token)
    {
        var f = Builders<BsonDocument>.Filter;
        var pair = f.In("_id", new[] { $"{personA}|{personB}", $"{personB}|{personA}" });
        return await Blocks.Find(pair & f.Eq(_blockedField, true)).Project(Builders<BsonDocument>.Projection.Include("_id")).FirstOrDefaultAsync(token) is not null;
    }

    public async Task<IReadOnlySet<string>> BlockedWithAsync(string person, IEnumerable<string> others, CancellationToken token)
    {
        var ids = others.Where(x => !string.IsNullOrEmpty(x)).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0) return new HashSet<string>(StringComparer.Ordinal);
        var f = Builders<BsonDocument>.Filter;
        var pairs = ids.SelectMany(other => new[] { $"{person}|{other}", $"{other}|{person}" });
        var found = await Blocks.Find(f.In("_id", pairs) & f.Eq(_blockedField, true))
            .Project(Builders<BsonDocument>.Projection.Include("bloqueaId").Include("bloqueadoId")).ToListAsync(token);
        return found.Select(x => x["bloqueaId"].AsString == person ? x["bloqueadoId"].AsString : x["bloqueaId"].AsString).ToHashSet(StringComparer.Ordinal);
    }
}
