using Knock.Market.Api.Modules.Catalog;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace Knock.Market.Api.Modules.Tecnicos.Services;

internal interface ITechnicianExploreReader
{
    /// <summary>Cuántos técnicos visibles ofrecen cada oficio (solo los de <paramref name="trades"/> si no es nula).</summary>
    Task<IReadOnlyDictionary<string, int>> CountByTradeAsync(IReadOnlyCollection<string>? trades, CancellationToken cancellationToken);

    /// <summary>Técnicos visibles mejor calificados, con filtro opcional de oficios y de colonia base, hasta <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<TechnicianProfileRecord>> TopRatedAsync(IReadOnlyCollection<string>? trades, string? colonia, int limit, CancellationToken cancellationToken);

    /// <summary>Fotos de trabajos con permiso del cliente y moderadas, más recientes primero.</summary>
    Task<IReadOnlyList<WorkPhotoResponse>> RecentWorkPhotosAsync(int limit, CancellationToken cancellationToken);
}

/// <summary>
/// Explorar (spec 046, TEC-14, R-36 a R-38 y R-49): cinco bloques de hasta 10 elementos. <c>auto</c>, <c>agua</c> y <c>luz</c> son los oficios <c>mec</c>, <c>plo</c> y
/// <c>ele</c> del catálogo. Se guarda 1 minuto por filtro, colonia y si hay sesión (un booleano, nunca la persona) y se invalida al escribir un perfil.
/// </summary>
internal sealed class TechnicianExploreService(ITechnicianExploreReader reader, ICatalogApi catalog, IMemoryCache cache, TechnicianReadCache readCache)
{
    public const int BlockSize = 10;

    public static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(1);

    private static readonly Dictionary<string, string> _tradeByFilter = new() { ["auto"] = "mec", ["agua"] = "plo", ["luz"] = "ele" };

    public static bool IsKnownFilter(string? filter) => filter is null or ExploreQuery.AllFilter || _tradeByFilter.ContainsKey(filter);

    public async Task<ExploreOutcome> ExploreAsync(ExploreQuery query, bool signedIn, CancellationToken cancellationToken)
    {
        var filter = query.Filtro ?? ExploreQuery.AllFilter;
        var colonia = query.Colonia is null ? null : MongoDB.Bson.ObjectId.Parse(query.Colonia).ToString();
        var key = $"tecnicos:explorar:{filter}:{colonia}:{signedIn}";
        if (cache.TryGetValue(key, out ExploreResponse? cached) && cached is not null)
        {
            return new ExploreOutcome(ExploreStatus.Ok, cached);
        }

        if (colonia is not null && await catalog.FindNeighborhoodAsync(colonia, cancellationToken) is null)
        {
            return new ExploreOutcome(ExploreStatus.ColoniaNotFound, null);
        }

        var trades = _tradeByFilter.TryGetValue(filter, out var trade) ? new[] { trade } : null;
        var active = (await catalog.ListActiveTradesAsync(cancellationToken)).Where(t => trades is null || trades.Contains(t.Clave)).ToList();
        var counts = await reader.CountByTradeAsync(trades, cancellationToken);
        var popular = active.Select(t => new ExploreService(t.Clave, t.Nombre, counts.GetValueOrDefault(t.Clave)))
            .Where(s => s.Tecnicos > 0).OrderByDescending(s => s.Tecnicos).ThenBy(s => s.Nombre, StringComparer.Ordinal).Take(BlockSize).ToList();
        var ideas = active.Where(t => popular.TrueForAll(p => p.Clave != t.Clave)).OrderBy(t => t.Nombre, StringComparer.Ordinal)
            .Take(BlockSize).Select(t => new ExploreIdea(t.Clave, t.Nombre)).ToList();
        var best = (await reader.TopRatedAsync(trades, null, BlockSize, cancellationToken)).Select(TechnicianLookupService.ToResponse).ToList();
        var response = new ExploreResponse(filter, popular, best, ideas, null, null);
        if (signedIn)
        {
            var near = colonia is null ? [] : (await reader.TopRatedAsync(trades, colonia, BlockSize, cancellationToken)).Select(TechnicianLookupService.ToResponse).ToList();
            response = response with { TrabajosReales = await reader.RecentWorkPhotosAsync(BlockSize, cancellationToken), CercaDeTi = near };
        }

        readCache.Set(cache, key, response, CacheLifetime);
        return new ExploreOutcome(ExploreStatus.Ok, response);
    }
}
