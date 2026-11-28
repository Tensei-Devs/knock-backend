using Knock.Hosting.Data;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using Knock.Market.Api.Modules.Tecnicos.Services;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Tecnicos.Data;

/// <summary>Posiciones aproximadas de técnicos visibles dentro de una caja (spec 046, TEC-04). Proyecta solo el identificador y el punto; nunca dirección ni contacto.</summary>
internal sealed class TechnicianMapStore(
    [FromKeyedServices(TecnicosModuleExtensions.ModuleKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> options,
    TimeProvider clock) : ITechnicianMapReader
{
    public async Task<IReadOnlyList<MapPoint>> FindPointsAsync(
        double south, double west, double north, double east, string? oficio, int limit, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var filter = TechnicianProfileStore.VisibleFilter(clock.GetUtcNow().UtcDateTime) & filters.GeoWithinBox("ubicacionBase", west, south, east, north);
        if (oficio is not null)
        {
            filter &= filters.AnyIn("oficios", [oficio]);
        }

        var documents = await database.GetCollection<BsonDocument>(TechnicianProfileStore.CollectionName)
            .Find(filter, new FindOptions { MaxTime = options.Value.QueryMaxTime })
            .Project(Builders<BsonDocument>.Projection.Include("ubicacionBase")).Limit(limit).ToListAsync(cancellationToken);
        return [.. documents.Select(ToPoint).OfType<MapPoint>()];
    }

    internal static MapPoint? ToPoint(BsonDocument d)
    {
        var coordinates = d.TryGetValue("ubicacionBase", out var point) && point.IsBsonDocument ? point.AsBsonDocument.GetValue("coordinates", BsonNull.Value) : BsonNull.Value;
        return coordinates.IsBsonArray && coordinates.AsBsonArray.Count == 2 && coordinates.AsBsonArray.All(v => v.IsNumeric)
            ? new MapPoint(d["_id"].AsObjectId, coordinates.AsBsonArray[1].ToDouble(), coordinates.AsBsonArray[0].ToDouble())
            : null;
    }
}
