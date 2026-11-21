using Knock.Market.Api.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Catalog.Data;

/// <summary>
/// Lecturas agrupadas del panel sobre <c>oficios_pedidos</c> y <c>listas_espera</c> (spec 045, OFP-02 y LIS-02). Agrupa y cuenta en la base y devuelve solo conteos: ninguna consulta proyecta
/// personas, celulares ni textos fuera del grupo (R-22). Cada página pide un elemento de más para saber si hay otra.
/// </summary>
internal sealed class CatalogAdminStore(
    [FromKeyedServices(CatalogModuleExtensions.ModuleKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> options)
{
    public const string TradeRequestsCollection = "oficios_pedidos";
    public const string WaitlistCollection = "listas_espera";
    private const string _idField = "_id";
    private const string _totalField = "total";

    private AggregateOptions Aggregate => new() { MaxTime = options.Value.QueryMaxTime };

    /// <summary>Oficios pedidos por <c>textoNormalizado</c>, de más a menos pedidos y, a igual cantidad, por texto. El cursor es el último grupo leído.</summary>
    public async Task<IReadOnlyList<(string Text, long Count)>> ListTradeRequestsAsync((long Count, string Key)? after, int take, CancellationToken cancellationToken)
    {
        var pipeline = new List<BsonDocument>
        {
            new("$group", new BsonDocument { { _idField, "$textoNormalizado" }, { _totalField, new BsonDocument("$sum", 1) } }),
        };
        if (after is { } position)
        {
            pipeline.Add(new BsonDocument("$match", new BsonDocument("$or", new BsonArray
            {
                new BsonDocument(_totalField, new BsonDocument("$lt", position.Count)),
                new BsonDocument { { _totalField, position.Count }, { _idField, new BsonDocument("$gt", position.Key) } },
            })));
        }

        pipeline.Add(new BsonDocument("$sort", new BsonDocument { { _totalField, -1 }, { _idField, 1 } }));
        pipeline.Add(new BsonDocument("$limit", take));
        using var cursor = await database.GetCollection<BsonDocument>(TradeRequestsCollection)
            .AggregateAsync<BsonDocument>(pipeline, Aggregate, cancellationToken);
        var rows = await cursor.ToListAsync(cancellationToken);
        return [.. rows.Select(row => (row[_idField].AsString, row[_totalField].ToInt64()))];
    }

    /// <summary>Personas en espera por código postal, ordenado por código postal. El cursor es el último código leído.</summary>
    public async Task<IReadOnlyList<(string PostalCode, long Count)>> ListWaitlistZonesAsync(string? afterPostalCode, int take, CancellationToken cancellationToken)
    {
        var pipeline = new List<BsonDocument>
        {
            new("$group", new BsonDocument { { _idField, "$cp" }, { _totalField, new BsonDocument("$sum", 1) } }),
        };
        if (afterPostalCode is not null)
        {
            pipeline.Add(new BsonDocument("$match", new BsonDocument(_idField, new BsonDocument("$gt", afterPostalCode))));
        }

        pipeline.Add(new BsonDocument("$sort", new BsonDocument(_idField, 1)));
        pipeline.Add(new BsonDocument("$limit", take));
        using var cursor = await database.GetCollection<BsonDocument>(WaitlistCollection)
            .AggregateAsync<BsonDocument>(pipeline, Aggregate, cancellationToken);
        var rows = await cursor.ToListAsync(cancellationToken);
        return [.. rows.Select(row => (row[_idField].AsString, row[_totalField].ToInt64()))];
    }
}
