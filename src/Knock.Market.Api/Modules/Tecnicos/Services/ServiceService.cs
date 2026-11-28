using Knock.Market.Api.Modules.Catalog;
using Knock.Market.Api.Modules.Tecnicos.Data;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;

namespace Knock.Market.Api.Modules.Tecnicos.Services;

/// <summary>Resultado de una operación sobre los servicios propios.</summary>
internal enum ServiceStatus
{
    Ok,
    NotATechnician,
    TradeInvalid,
    LimitReached,
    NotFound,
}

/// <summary>Resultado con el servicio, si lo hay.</summary>
internal sealed record ServiceOutcome(ServiceStatus Status, TechnicianService? Service = null)
{
    public sealed override string ToString() => nameof(ServiceOutcome);
}

/// <summary>
/// Servicios y precios de referencia del técnico (spec 047, SRV-01 a SRV-04). La persona es siempre la del <c>sub</c> del token (R-18); un servicio ajeno responde como inexistente (R-15). El oficio
/// debe estar activo en el catálogo y entre los del técnico (R-13). No toca MongoDB: pasa por <see cref="IServiceStore"/>.
/// </summary>
internal sealed class ServiceService(IServiceStore store, ICatalogApi catalog, IMemoryCache cache)
{
    /// <summary>Vida de la lista de servicios en la caché de la réplica (R-04 y R-16).</summary>
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(1);

    /// <summary>Clave de la lista: solo lleva el identificador del técnico, nunca datos de quien consulta (R-04).</summary>
    public static string CacheKey(ObjectId technicianId) => $"tecnicos:servicios:{technicianId}";

    /// <summary>Los servicios de un técnico visible; <see langword="null"/> si no existe o está oculto (404 <c>technician_not_found</c>).</summary>
    public async Task<TechnicianServiceList?> ListAsync(string technicianId, CancellationToken cancellationToken)
    {
        var id = ObjectId.Parse(technicianId);
        var key = CacheKey(id);
        if (cache.TryGetValue(key, out TechnicianServiceList? cached))
        {
            return cached;
        }

        if (!await store.IsVisibleAsync(id, cancellationToken))
        {
            return null;
        }

        var list = new TechnicianServiceList(await store.ListAsync(id, cancellationToken));
        cache.Set(key, list, CacheLifetime);
        return list;
    }

    public async Task<ServiceOutcome> CreateAsync(string sub, CreateServiceBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (await store.FindOwnAsync(sub, cancellationToken) is not { } own)
        {
            return new ServiceOutcome(ServiceStatus.NotATechnician);
        }

        if (!await TradeAllowedAsync(own.Trades, body.Oficio!, cancellationToken))
        {
            return new ServiceOutcome(ServiceStatus.TradeInvalid);
        }

        var (status, service) = await store.CreateAsync(own.Id, body.Nombre!.Trim(), body.Detalle?.Trim(), body.PrecioReferencia!.Value, body.Oficio!, cancellationToken);
        if (status != ServiceCreateStatus.Created)
        {
            return new ServiceOutcome(ServiceStatus.LimitReached);
        }

        cache.Remove(CacheKey(own.Id));
        return new ServiceOutcome(ServiceStatus.Ok, service);
    }

    public async Task<ServiceOutcome> UpdateAsync(string sub, string serviceId, UpdateServiceBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (await store.FindOwnAsync(sub, cancellationToken) is not { } own)
        {
            return new ServiceOutcome(ServiceStatus.NotATechnician);
        }

        if (body.Oficio is not null && !await TradeAllowedAsync(own.Trades, body.Oficio, cancellationToken))
        {
            return new ServiceOutcome(ServiceStatus.TradeInvalid);
        }

        var service = await store.UpdateAsync(own.Id, ObjectId.Parse(serviceId), body.Nombre?.Trim(), body.Detalle?.Trim(), body.PrecioReferencia, body.Oficio, cancellationToken);
        if (service is null)
        {
            return new ServiceOutcome(ServiceStatus.NotFound);
        }

        cache.Remove(CacheKey(own.Id));
        return new ServiceOutcome(ServiceStatus.Ok, service);
    }

    public async Task<ServiceOutcome> DeleteAsync(string sub, string serviceId, CancellationToken cancellationToken)
    {
        if (await store.FindOwnAsync(sub, cancellationToken) is not { } own)
        {
            return new ServiceOutcome(ServiceStatus.NotATechnician);
        }

        if (!await store.DeleteAsync(own.Id, ObjectId.Parse(serviceId), cancellationToken))
        {
            return new ServiceOutcome(ServiceStatus.NotFound);
        }

        cache.Remove(CacheKey(own.Id));
        return new ServiceOutcome(ServiceStatus.Ok);
    }

    private async Task<bool> TradeAllowedAsync(IReadOnlyList<string> own, string trade, CancellationToken cancellationToken) =>
        own.Contains(trade, StringComparer.Ordinal) && await catalog.FindActiveTradeAsync(trade, cancellationToken) is not null;
}
