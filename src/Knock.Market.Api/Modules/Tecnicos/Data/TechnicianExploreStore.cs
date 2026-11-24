using Knock.Hosting.Data;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using Knock.Market.Api.Modules.Tecnicos.Services;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Tecnicos.Data;

/// <summary>Lecturas de Explorar (spec 046, TEC-14). Solo técnicos visibles; nunca proyecta dirección ni contacto.</summary>
internal sealed class TechnicianExploreStore(
    [FromKeyedServices(TecnicosModuleExtensions.ModuleKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> options,
    TimeProvider clock) : ITechnicianExploreReader
{
    private FindOptions Find => new() { MaxTime = options.Value.QueryMaxTime };

    private IMongoCollection<BsonDocument> Technicians => database.GetCollection<BsonDocument>(TechnicianProfileStore.CollectionName);

    public async Task<IReadOnlyDictionary<string, int>> CountByTradeAsync(IReadOnlyCollection<string>? trades, CancellationToken cancellationToken)
    {
        var documents = await Technicians.Find(Visible(trades, null), Find).Project(Builders<BsonDocument>.Projection.Include("oficios")).ToListAsync(cancellationToken);
        return documents.Where(d => d.GetValue("oficios", BsonNull.Value).IsBsonArray)
            .SelectMany(d => d["oficios"].AsBsonArray.Where(v => v.IsString).Select(v => v.AsString).Distinct())
            .Where(t => trades is null || trades.Contains(t)).GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<IReadOnlyList<TechnicianProfileRecord>> TopRatedAsync(IReadOnlyCollection<string>? trades, string? colonia, int limit, CancellationToken cancellationToken)
    {
        var projection = Builders<BsonDocument>.Projection.Combine(TechnicianProfileStore.ProfileFields.Select(f => Builders<BsonDocument>.Projection.Include(f)));
        var documents = await Technicians.Find(Visible(trades, colonia), Find).Project(projection)
            .Sort(Builders<BsonDocument>.Sort.Descending("calificacion").Descending("_id")).Limit(limit).ToListAsync(cancellationToken);
        return [.. documents.Select(TechnicianProfileStore.ToProfile)];
    }

    public async Task<IReadOnlyList<WorkPhotoResponse>> RecentWorkPhotosAsync(int limit, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var documents = await database.GetCollection<BsonDocument>(WorkPhotoStore.CollectionName)
            .Find(filters.Eq("permiso", true) & filters.Eq("moderada", true), Find)
            .Project(Builders<BsonDocument>.Projection.Include("url").Include("creadoEn")).Sort(Builders<BsonDocument>.Sort.Descending("_id")).Limit(limit).ToListAsync(cancellationToken);
        return [.. documents.Where(d => d.GetValue("url", BsonNull.Value).IsString).Select(WorkPhotoStore.ToPhoto)];
    }

    private FilterDefinition<BsonDocument> Visible(IReadOnlyCollection<string>? trades, string? colonia)
    {
        var filters = Builders<BsonDocument>.Filter;
        var filter = TechnicianProfileStore.VisibleFilter(clock.GetUtcNow().UtcDateTime);
        if (trades is not null)
        {
            filter &= filters.AnyIn("oficios", trades);
        }

        return colonia is null ? filter : filter & filters.Eq("coloniaBaseId", colonia);
    }
}
