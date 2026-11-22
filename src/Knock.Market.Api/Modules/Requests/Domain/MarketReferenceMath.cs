using Knock.Market.Api.Modules.Requests.Dtos;

namespace Knock.Market.Api.Modules.Requests.Domain;

/// <summary>Cálculo de la referencia de mercado (spec 048, R-12): percentiles 25 y 75 y mediana por interpolación lineal, en pesos enteros.</summary>
internal static class MarketReferenceMath
{
    /// <summary>Cotizaciones aceptadas que se necesitan para publicar un rango (D-06).</summary>
    public const int MinimumSamples = 10;

    public const int WindowDays = 90;

    public static MarketReference? Summarize(IReadOnlyList<int> prices, string origin)
    {
        ArgumentNullException.ThrowIfNull(prices);
        if (prices.Count < MinimumSamples) return null;
        var sorted = prices.Order().ToArray();
        return new MarketReference(origin, Percentile(sorted, 0.25), Percentile(sorted, 0.5), Percentile(sorted, 0.75));
    }

    internal static int Percentile(int[] sorted, double fraction)
    {
        var position = (sorted.Length - 1) * fraction;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        return (int)Math.Round(sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower), MidpointRounding.AwayFromZero);
    }
}
