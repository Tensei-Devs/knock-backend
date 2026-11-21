using Knock.Market.Api.Modules.Catalog;
using Knock.Market.Api.Modules.Tecnicos.Data;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
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
/// debe estar activo en el catálogo y entre los del técnico (R-13). No toca MongoDB: pasa por <see cref="ServiceStore"/>.
/// </summary>
internal sealed class ServiceService(ServiceStore store, ICatalogApi catalog)
{
    /// <summary>Los servicios de un técnico visible; <see langword="null"/> si no existe o está oculto (404 <c>technician_not_found</c>).</summary>
    public async Task<TechnicianServiceList?> ListAsync(string technicianId, CancellationToken cancellationToken)
    {
        var id = ObjectId.Parse(technicianId);
        return await store.IsVisibleAsync(id, cancellationToken) ? new TechnicianServiceList(await store.ListAsync(id, cancellationToken)) : null;
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
        return status == ServiceCreateStatus.Created ? new ServiceOutcome(ServiceStatus.Ok, service) : new ServiceOutcome(ServiceStatus.LimitReached);
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
        return service is null ? new ServiceOutcome(ServiceStatus.NotFound) : new ServiceOutcome(ServiceStatus.Ok, service);
    }

    public async Task<ServiceOutcome> DeleteAsync(string sub, string serviceId, CancellationToken cancellationToken)
    {
        if (await store.FindOwnAsync(sub, cancellationToken) is not { } own)
        {
            return new ServiceOutcome(ServiceStatus.NotATechnician);
        }

        return await store.DeleteAsync(own.Id, ObjectId.Parse(serviceId), cancellationToken) ? new ServiceOutcome(ServiceStatus.Ok) : new ServiceOutcome(ServiceStatus.NotFound);
    }

    private async Task<bool> TradeAllowedAsync(IReadOnlyList<string> own, string trade, CancellationToken cancellationToken) =>
        own.Contains(trade, StringComparer.Ordinal) && await catalog.FindActiveTradeAsync(trade, cancellationToken) is not null;
}
