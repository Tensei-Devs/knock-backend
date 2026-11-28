using System.Globalization;
using Knock.Hosting.Data;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using Knock.Market.Api.Modules.Tecnicos.Services;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Tecnicos.Data;

/// <summary>
/// Lee <c>tecnicos.reputacion</c> (spec 046, TEC-13): <c>{ promedio, total, distribucion: { "1": n, … "5": n }, nivel }</c>, calculado por la spec 051. Filtra por
/// <c>usuarioId</c> = <c>sub</c> del token (R-50). Sin documento <c>reputacion</c> devuelve el nivel <c>nuevo</c> sin opiniones.
/// </summary>
internal sealed class TechnicianReputationStore(
    [FromKeyedServices(TecnicosModuleExtensions.ModuleKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> options) : IReputationReader
{
    public const string NewLevel = "nuevo";

    public async Task<ReputationResponse?> FindOwnAsync(string sub, CancellationToken cancellationToken)
    {
        var d = await database.GetCollection<BsonDocument>(TechnicianProfileStore.CollectionName)
            .Find(Builders<BsonDocument>.Filter.Eq("usuarioId", sub), new FindOptions { MaxTime = options.Value.QueryMaxTime })
            .Project(Builders<BsonDocument>.Projection.Include("reputacion")).FirstOrDefaultAsync(cancellationToken);
        return d is null ? null : ToReputation(d.GetValue("reputacion", BsonNull.Value));
    }

    internal static ReputationResponse ToReputation(BsonValue value)
    {
        var stars = Enumerable.Range(1, 5).ToDictionary(n => n.ToString(CultureInfo.InvariantCulture), _ => 0);
        if (!value.IsBsonDocument)
        {
            return new ReputationResponse(null, 0, stars, NewLevel);
        }

        var reputation = value.AsBsonDocument;
        if (reputation.GetValue("distribucion", BsonNull.Value) is { IsBsonDocument: true } distribution)
        {
            foreach (var star in stars.Keys.ToList())
            {
                var count = distribution.AsBsonDocument.GetValue(star, BsonNull.Value);
                if (count.IsNumeric)
                {
                    stars[star] = Math.Max(0, count.ToInt32());
                }
            }
        }

        var average = reputation.GetValue("promedio", BsonNull.Value);
        var total = reputation.GetValue("total", BsonNull.Value);
        var level = reputation.GetValue("nivel", BsonNull.Value);
        return new ReputationResponse(
            average.IsNumeric ? Math.Round(average.ToDouble(), 1, MidpointRounding.AwayFromZero) : null,
            total.IsNumeric ? Math.Max(0, total.ToInt32()) : stars.Values.Sum(),
            stars,
            level.IsString ? level.AsString : NewLevel);
    }
}
