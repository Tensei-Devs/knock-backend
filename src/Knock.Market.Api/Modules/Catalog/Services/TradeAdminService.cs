using System.Globalization;
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
internal sealed class TradeAdminService(ITradeAdminStore store, IActiveOficios catalog, IPanelAuditTrail audit, TimeProvider clock)
{
    public async Task<AdminTradeListResponse> ListAsync(CancellationToken cancellationToken) =>
        new([.. (await store.ListAllAsync(cancellationToken)).Select(ToResponse)]);

    /// <summary>
    /// Alta de un oficio (OFI-04). Primero la auditoría (quién, cuándo y qué): sin ella no se escribe nada (<see cref="PanelAuditUnavailableException"/>). La clave repetida lanza 409 <c>trade_key_taken</c>.
    /// Después de escribir invalida la caché de oficios activos para que la lista y el detalle lo vean enseguida.
    /// </summary>
    public async Task<AdminTradeResponse> CreateAsync(string panelSubject, CreateTradeBody body, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(panelSubject);
        ArgumentNullException.ThrowIfNull(body);
        var trade = new NewTrade(
            body.Clave!, body.Nombre!.Trim(), body.Modalidad!, [.. (body.FrasesSugeridas ?? []).Select(phrase => phrase.Trim())],
            body.TopePorcentaje!.Value, body.TopeFijoPorcentaje!.Value, body.PisoAprobacion!.Value, body.VisitaMaxima!.Value, body.Activo ?? true);
        if (!await audit.RecordAsync(new PanelAuditEntry(panelSubject, "trade.create", trade.Clave, clock.GetUtcNow(), Describe(trade)), cancellationToken))
        {
            throw new PanelAuditUnavailableException();
        }

        if (!await store.InsertAsync(trade, cancellationToken))
        {
            throw new DomainException(DomainErrorKind.Conflict, "A trade with that key already exists.", TradeErrorCodes.TradeKeyTakenCode);
        }

        await catalog.InvalidateActiveOficiosAsync(cancellationToken);
        return new AdminTradeResponse(
            trade.Clave, trade.Nombre, trade.Modalidad, trade.FrasesSugeridas, trade.TopePorcentaje, trade.TopeFijoPorcentaje, trade.PisoAprobacion, trade.VisitaMaxima, trade.Activo);
    }

    private static Dictionary<string, string> Describe(NewTrade trade) => new(StringComparer.Ordinal)
    {
        ["nombre"] = trade.Nombre,
        ["modalidad"] = trade.Modalidad,
        ["topePorcentaje"] = trade.TopePorcentaje.ToString(CultureInfo.InvariantCulture),
        ["topeFijoPorcentaje"] = trade.TopeFijoPorcentaje.ToString(CultureInfo.InvariantCulture),
        ["pisoAprobacion"] = trade.PisoAprobacion.ToString(CultureInfo.InvariantCulture),
        ["visitaMaxima"] = trade.VisitaMaxima.ToString(CultureInfo.InvariantCulture),
        ["activo"] = trade.Activo ? "true" : "false",
    };

    internal static AdminTradeResponse ToResponse(AdminTrade trade) => new(
        trade.Clave, trade.Nombre, trade.Modalidad, trade.FrasesSugeridas, trade.TopePorcentaje, trade.TopeFijoPorcentaje, trade.PisoAprobacion, trade.VisitaMaxima, trade.Activo);
}
