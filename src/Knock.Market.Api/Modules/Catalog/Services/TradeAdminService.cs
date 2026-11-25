using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Catalog.Data;
using Knock.Market.Api.Modules.Catalog.Domain;
using Knock.Market.Api.Modules.Catalog.Dtos;

namespace Knock.Market.Api.Modules.Catalog.Services;

/// <summary>
/// Administración del catálogo de oficios desde el panel (spec 045, OFI-03 a OFI-05). La autorización (permiso <c>catalog:manage</c>) la resuelve la política de la ruta antes de llegar aquí; este servicio
/// aplica las reglas del oficio, deja la auditoría y invalida la caché de oficios activos al escribir. No toca MongoDB: pasa por <see cref="ITradeAdminStore"/>.
/// </summary>
internal sealed class TradeAdminService(ITradeAdminStore store, IActiveOficios catalog, TimeProvider clock)
{
    public async Task<AdminTradeListResponse> ListAsync(CancellationToken cancellationToken) =>
        new([.. (await store.ListAllAsync(cancellationToken)).Select(ToResponse)]);

    /// <summary>
    /// Alta de un oficio (OFI-04). La auditoría (quién, cuándo y qué) se escribe en la bandeja de salida dentro de la misma transacción que el oficio (spec 045, R-20): o quedan los dos o ninguno. La clave
    /// repetida lanza 409 <c>trade_key_taken</c> y no deja evento.
    /// Después de escribir invalida la caché de oficios activos para que la lista y el detalle lo vean enseguida.
    /// </summary>
    public async Task<AdminTradeResponse> CreateAsync(string panelSubject, CreateTradeBody body, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(panelSubject);
        ArgumentNullException.ThrowIfNull(body);
        var trade = new NewTrade(
            body.Clave!, body.Nombre!.Trim(), body.Modalidad!, [.. (body.FrasesSugeridas ?? []).Select(phrase => phrase.Trim())],
            body.TopePorcentaje!.Value, body.TopeFijoPorcentaje!.Value, body.PisoAprobacion!.Value, body.VisitaMaxima!.Value, body.Activo ?? true);
        if (!await store.InsertAsync(trade, new TradeAuditEvent(panelSubject, Created: true, clock.GetUtcNow().UtcDateTime), cancellationToken))
        {
            throw new DomainException(DomainErrorKind.Conflict, "A trade with that key already exists.", TradeErrorCodes.TradeKeyTakenCode);
        }

        await catalog.InvalidateActiveOficiosAsync(cancellationToken);
        return new AdminTradeResponse(
            trade.Clave, trade.Nombre, trade.Modalidad, trade.FrasesSugeridas, trade.TopePorcentaje, trade.TopeFijoPorcentaje, trade.PisoAprobacion, trade.VisitaMaxima, trade.Activo);
    }

    /// <summary>
    /// Edición parcial de un oficio (OFI-05). La auditoría se escribe en la misma transacción que el cambio; un oficio que no existe lanza 404 <c>trade_not_found</c>. La clave no cambia nunca. Después de escribir invalida la caché de
    /// oficios activos: la siguiente consulta pública ya ve el cambio (R-19).
    /// </summary>
    public async Task<AdminTradeResponse> UpdateAsync(string panelSubject, string clave, UpdateTradeBody body, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(panelSubject);
        ArgumentException.ThrowIfNullOrEmpty(clave);
        ArgumentNullException.ThrowIfNull(body);
        var changes = new TradeChanges(
            body.Nombre?.Trim(), body.Modalidad, body.FrasesSugeridas is null ? null : [.. body.FrasesSugeridas.Select(phrase => phrase.Trim())],
            body.TopePorcentaje, body.TopeFijoPorcentaje, body.PisoAprobacion, body.VisitaMaxima, body.Activo);
        var updated = await store.UpdateAsync(clave, changes, new TradeAuditEvent(panelSubject, Created: false, clock.GetUtcNow().UtcDateTime), cancellationToken)
            ?? throw new DomainException(DomainErrorKind.NotFound, "The trade does not exist.", TradeErrorCodes.TradeNotFoundCode);
        await catalog.InvalidateActiveOficiosAsync(cancellationToken);
        return ToResponse(updated);
    }

    internal static AdminTradeResponse ToResponse(AdminTrade trade) => new(
        trade.Clave, trade.Nombre, trade.Modalidad, trade.FrasesSugeridas, trade.TopePorcentaje, trade.TopeFijoPorcentaje, trade.PisoAprobacion, trade.VisitaMaxima, trade.Activo);
}
