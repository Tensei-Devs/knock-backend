using Asp.Versioning;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Catalog.Dtos;
using Knock.Market.Api.Modules.Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Market.Api.Modules.Catalog.Controllers.V1;

/// <summary>
/// Administración del catálogo de oficios desde el panel (spec 045, OFI-03 a OFI-05). Exigen el token del panel y el permiso <c>catalog:manage</c> (PROVISIONAL, ver <see cref="PanelAuthentication"/>):
/// sin sesión 401, sin permiso 403. Nunca se cachean. Mientras no exista la puerta real de permisos del panel (spec 033), <see cref="DenyAllPanelPermissionGate"/> niega todo y estas rutas responden 403.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/oficios")]
[Authorize(AuthenticationSchemes = PanelAuthentication.Scheme, Policy = PanelAuthentication.CatalogManagePolicy)]
[PrivateNoStore]
[EnableRateLimiting(RateLimitingExtensions.AdminPolicy)]
internal sealed class AdminTradesController(TradeAdminService service) : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>Todos los oficios, activos e inactivos, con sus parámetros de precio (OFI-03).</summary>
    [HttpGet]
    [ProducesResponseType<AdminTradeListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<ActionResult<AdminTradeListResponse>> List(CancellationToken cancellationToken) =>
        await service.ListAsync(cancellationToken);
}
