using System.Globalization;
using Knock.Market.Api.Modules.Catalog;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace Knock.Market.Api.Modules.Tecnicos.Services;

internal interface ITechnicianMapReader
{
    /// <summary>Técnicos visibles (con o sin <paramref name="oficio"/>) cuya ubicación aproximada cae en la caja, hasta <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<MapPoint>> FindPointsAsync(double south, double west, double north, double east, string? oficio, int limit, CancellationToken cancellationToken);
}

/// <summary>
/// Mapa de técnicos (spec 046, TEC-04, R-11 a R-13 y R-40). La zona pedida se ajusta a celdas de la cuadrícula del zoom: con ellas se consulta y se arma la clave de caché,
/// así que ninguna coordenada de la petición se guarda ni forma parte de la clave. Los técnicos se agrupan por celda y se devuelven hasta 200 grupos. La caché dura unos
/// segundos y se invalida al escribir un perfil.
/// </summary>
internal sealed class TechnicianMapService(ITechnicianMapReader reader, ICatalogApi catalog, IMemoryCache cache, TechnicianReadCache readCache)
{
    public const int MaxGroups = 200;

    /// <summary>Tope de puntos que se leen para agrupar; más allá, el mapa ya no distingue grupos útiles.</summary>
    public const int MaxPoints = 5000;

    public static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(15);

    /// <summary>Lado de la celda en grados: 180 / 2^zoom (zoom 12 son unos 5 km).</summary>
    internal static double CellDegrees(int zoom) => 180.0 / Math.Pow(2, zoom);

    public async Task<MapOutcome> SearchAsync(MapQuery query, CancellationToken cancellationToken)
    {
        var zoom = query.Zoom is null ? MapQuery.DefaultZoom : int.Parse(query.Zoom, CultureInfo.InvariantCulture);
        var cell = CellDegrees(zoom);
        var south = (long)Math.Floor(Parse(query.Sur) / cell);
        var north = (long)Math.Ceiling(Parse(query.Norte) / cell);
        var west = (long)Math.Floor(Parse(query.Oeste) / cell);
        var east = (long)Math.Ceiling(Parse(query.Este) / cell);
        var key = $"tecnicos:mapa:{zoom}:{query.Oficio ?? string.Empty}:{south}:{north}:{west}:{east}";
        if (cache.TryGetValue(key, out MapResponse? cached) && cached is not null)
        {
            return new MapOutcome(MapStatus.Ok, cached);
        }

        if (query.Oficio is not null && await catalog.FindActiveTradeAsync(query.Oficio, cancellationToken) is null)
        {
            return new MapOutcome(MapStatus.OficioInvalid, null);
        }

        var points = await reader.FindPointsAsync(south * cell, west * cell, north * cell, east * cell, query.Oficio, MaxPoints, cancellationToken);
        var response = new MapResponse(Group(points, cell));
        readCache.Set(cache, key, response, CacheLifetime);
        return new MapOutcome(MapStatus.Ok, response);
    }

    internal static IReadOnlyList<MapGroup> Group(IEnumerable<MapPoint> points, double cell) =>
        [.. points.GroupBy(p => (Row: (long)Math.Floor(p.Latitude / cell), Column: (long)Math.Floor(p.Longitude / cell)))
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Row).ThenBy(g => g.Key.Column)
            .Take(MaxGroups)
            .Select(g => new MapGroup(
                Math.Round((g.Key.Row + 0.5) * cell, 6),
                Math.Round((g.Key.Column + 0.5) * cell, 6),
                g.Count(),
                g.Count() == 1 ? g.First().Id.ToString() : null))];

    private static double Parse(string? value) => double.Parse(value!, NumberStyles.Float, CultureInfo.InvariantCulture);
}
