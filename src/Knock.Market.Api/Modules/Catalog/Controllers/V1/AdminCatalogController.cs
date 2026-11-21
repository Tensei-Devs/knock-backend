using Asp.Versioning;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Catalog.Dtos;
using Knock.Market.Api.Modules.Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Market.Api.Modules.Catalog.Controllers.V1;

/// <summary>
/// Consultas internas del panel sobre oficios pedidos y lista de espera (spec 045, OFP-02 y LIS-02; HU-070). Exigen el token del panel y el permiso <c>metrics:read</c> (PROVISIONAL, ver
/// <see cref="PanelAuthentication"/>): sin sesión 401, sin permiso 403. Solo devuelven conteos y nunca se cachean.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize(AuthenticationSchemes = PanelAuthentication.Scheme, Policy = PanelAuthentication.MetricsReadPolicy)]
[PrivateNoStore]
[EnableRateLimiting(RateLimitingExtensions.AdminPolicy)]
internal sealed class AdminCatalogController(CatalogAdminService service) : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>Oficios pedidos en «Otro oficio», agrupados por texto normalizado y contados, de más a menos pedidos, con paginación por cursor (20 por omisión, 50 como máximo).</summary>
    [HttpGet("oficios-pedidos")]
    [ProducesResponseType<TradeRequestPage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<ActionResult<TradeRequestPage>> ListTradeRequests([FromQuery] AdminPageQuery query, CancellationToken cancellationToken) =>
        await service.ListTradeRequestsAsync(query, cancellationToken);

    /// <summary>Cuántas personas esperan por código postal, ordenado por código postal, con paginación por cursor. Nunca incluye celulares.</summary>
    [HttpGet("lista-espera")]
    [ProducesResponseType<WaitlistZonePage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<ActionResult<WaitlistZonePage>> ListWaitlist([FromQuery] AdminPageQuery query, CancellationToken cancellationToken) =>
        await service.ListWaitlistZonesAsync(query, cancellationToken);
}
