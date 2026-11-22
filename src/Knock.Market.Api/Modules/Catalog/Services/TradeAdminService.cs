using Knock.Market.Api.Modules.Catalog.Data;
using Knock.Market.Api.Modules.Catalog.Dtos;

namespace Knock.Market.Api.Modules.Catalog.Services;

/// <summary>
/// Administración del catálogo de oficios desde el panel (spec 045, OFI-03 a OFI-05). La autorización (permiso <c>catalog:manage</c>) la resuelve la política de la ruta antes de llegar aquí; este servicio
/// aplica las reglas del oficio, deja la auditoría y invalida la caché de oficios activos al escribir. No toca MongoDB: pasa por <see cref="ITradeAdminStore"/>.
/// </summary>
internal sealed class TradeAdminService(ITradeAdminStore store)
{
    public async Task<AdminTradeListResponse> ListAsync(CancellationToken cancellationToken) =>
        new([.. (await store.ListAllAsync(cancellationToken)).Select(ToResponse)]);

    internal static AdminTradeResponse ToResponse(AdminTrade trade) => new(
        trade.Clave, trade.Nombre, trade.Modalidad, trade.FrasesSugeridas, trade.TopePorcentaje, trade.TopeFijoPorcentaje, trade.PisoAprobacion, trade.VisitaMaxima, trade.Activo);
}
