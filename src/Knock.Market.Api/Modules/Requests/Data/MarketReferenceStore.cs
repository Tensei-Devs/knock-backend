using Knock.Market.Api.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Requests.Data;

/// <summary>Lee los precios de las cotizaciones aceptadas de un oficio (spec 048, COT-03). Devuelve solo montos, sin identificadores de técnico, cliente ni solicitud.</summary>
internal sealed class MarketReferenceStore(
    [FromKeyedServices(RequestsModuleExtensions.ModuleKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> options)
{
    private const int _maxSamples = 5000;

    public async Task<IReadOnlyList<int>> ListAcceptedPricesAsync(string trade, string? priceType, DateTime sinceUtc, CancellationToken cancellationToken)
    {
        var accepted = new BsonDocument { ["estado"] = "aceptada", ["aceptadaEn"] = new BsonDocument("$gte", sinceUtc) };
        if (priceType is not null) accepted["tipoPrecio"] = priceType;
        var stages = new[]
        {
            new BsonDocument("$match", accepted),
            new BsonDocument("$lookup", new BsonDocument { ["from"] = "solicitudes", ["localField"] = "solicitudId", ["foreignField"] = "_id", ["as"] = "s" }),
            new BsonDocument("$match", new BsonDocument("s.oficioClave", trade)),
            new BsonDocument("$project", new BsonDocument { ["_id"] = 0, ["precio"] = 1, ["precioMin"] = 1, ["precioMax"] = 1 }),
            new BsonDocument("$limit", _maxSamples),
        };
        using var cursor = await database.GetCollection<BsonDocument>("cotizaciones")
            .AggregateAsync<BsonDocument>(stages, new AggregateOptions { MaxTime = options.Value.QueryMaxTime }, cancellationToken);
        var documents = await cursor.ToListAsync(cancellationToken);
        return [.. documents.Select(Price).Where(x => x is not null).Select(x => x!.Value)];
    }

    private static int? Price(BsonDocument document)
    {
        static int? Amount(BsonDocument doc, string field) => doc.TryGetValue(field, out var value) && value.IsNumeric ? value.ToInt32() : null;
        if (Amount(document, "precio") is { } price) return price;
        var min = Amount(document, "precioMin");
        var max = Amount(document, "precioMax");
        if (min is not { } low) return null;
        return max is { } high ? (low + high) / 2 : low;
    }
}
