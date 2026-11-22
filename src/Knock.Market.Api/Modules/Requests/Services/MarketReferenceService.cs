using Knock.Market.Api.Modules.Requests.Data;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace Knock.Market.Api.Modules.Requests.Services;

/// <summary>Caché de la referencia de mercado de esta réplica: una versión que sube al elegir una cotización deja sin uso las entradas anteriores (spec 048, R-06).</summary>
internal sealed class MarketReferenceCache(IMemoryCache cache)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    private long _version;

    public IMemoryCache Cache => cache;

    /// <summary>La clave lleva solo el oficio, el tipo y la versión; nunca datos de quien consulta ni de otra persona.</summary>
    public string Key(string trade, string? type) => $"cotizaciones:referencia:{Interlocked.Read(ref _version)}:{trade}:{type ?? "todos"}";

    public void Invalidate() => Interlocked.Increment(ref _version);
}

internal enum MarketReferenceStatus { Ok, NotATechnician }

internal sealed record MarketReferenceOutcome(MarketReferenceStatus Status, MarketReference? Reference = null)
{
    public sealed override string ToString() => nameof(MarketReferenceOutcome);
}

/// <summary>
/// Referencia de mercado (spec 048, COT-03). Con 10 o más cotizaciones aceptadas en 90 días del mismo oficio y tipo responde <c>mercado</c>; si no, con 10 o más del oficio
/// completo responde <c>categoria</c>; si tampoco, <c>sin_datos</c>. Solo la consulta un técnico, y se guarda 1 hora.
/// </summary>
internal sealed class MarketReferenceService(
    ITechnicianDirectory technicians, MarketReferenceStore store, MarketReferenceCache cache, TimeProvider clock)
{
    private static readonly MarketReference _noData = new("sin_datos", null, null, null);

    public async Task<MarketReferenceOutcome> GetAsync(string sub, MarketReferenceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!await technicians.ExistsAsync(sub, cancellationToken)) return new MarketReferenceOutcome(MarketReferenceStatus.NotATechnician);
        var trade = query.Oficio!;
        var key = cache.Key(trade, query.TipoTrabajo);
        if (cache.Cache.TryGetValue(key, out MarketReference? cached) && cached is not null)
            return new MarketReferenceOutcome(MarketReferenceStatus.Ok, cached);
        var since = clock.GetUtcNow().UtcDateTime.AddDays(-MarketReferenceMath.WindowDays);
        var reference = await ComputeAsync(trade, query.TipoTrabajo, since, cancellationToken);
        cache.Cache.Set(key, reference, MarketReferenceCache.Lifetime);
        return new MarketReferenceOutcome(MarketReferenceStatus.Ok, reference);
    }

    private async Task<MarketReference> ComputeAsync(string trade, string? type, DateTime since, CancellationToken cancellationToken)
    {
        var market = MarketReferenceMath.Summarize(await store.ListAcceptedPricesAsync(trade, type, since, cancellationToken), "mercado");
        if (market is not null || type is null) return market ?? _noData;
        return MarketReferenceMath.Summarize(await store.ListAcceptedPricesAsync(trade, null, since, cancellationToken), "categoria") ?? _noData;
    }
}
