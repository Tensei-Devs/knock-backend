using Asp.Versioning;
using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Catalog.Domain;
using Knock.Market.Api.Modules.Catalog.Dtos;
using Knock.Market.Api.Modules.Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Market.Api.Modules.Catalog.Controllers.V1;

/// <summary>
/// Administración del catálogo de oficios desde el panel (spec 045, OFI-03 a OFI-05). Exigen el token del panel y el permiso <c>catalog:manage</c> (PROVISIONAL, ver <see cref="PanelAuthentication"/>):
/// sin sesión 401, sin permiso 403. Nunca se cachean. Mientras no exista la puerta real de permisos del panel (spec 033), <see cref="DenyAllPanelPermissionGate"/> niega cualquier permiso y estas rutas responden 403.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/oficios")]
[Authorize(AuthenticationSchemes = PanelAuthentication.Scheme, Policy = PanelAuthentication.CatalogManagePolicy)]
[PrivateNoStore]
[EnableRateLimiting(MarketRateLimits.AdminPolicy)]
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

    /// <summary>
    /// Alta de un oficio (OFI-04): 201 con el oficio creado. La clave repetida responde 409 <c>trade_key_taken</c>; un cuerpo ilegible, 400; un valor inválido, 422. Deja la auditoría del panel antes de escribir:
    /// sin ella responde 503 <c>panel_audit_unavailable</c> y no cambia nada.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(TradeBodies.MaxBodyBytes)]
    [ProducesResponseType<AdminTradeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, _problemJson)]
    public async Task<IActionResult> Create([FromBody] CreateTradeBody body, CancellationToken cancellationToken)
    {
        if (User.FindFirst("sub")?.Value is not { Length: > 0 } subject)
        {
            return Unauthorized();
        }

        try
        {
            var created = await service.CreateAsync(subject, body, cancellationToken);
            return Created(new Uri($"/api/v1/oficios/{created.Clave}", UriKind.Relative), created);
        }
        catch (DomainException exception) when (exception.Code == TradeErrorCodes.AuditUnavailableCode)
        {
            return AuditUnavailable();
        }
    }

    /// <summary>
    /// Edición parcial de un oficio y sus parámetros de precio (OFI-05): 200 con el oficio como quedó. La clave no se puede cambiar: una <c>clave</c> distinta de la de la ruta responde 422 <c>errors.clave</c>.
    /// Un oficio que no existe responde 404 <c>trade_not_found</c>; la caché de oficios activos se invalida al escribir.
    /// </summary>
    [HttpPut("{clave}")]
    [RequestSizeLimit(TradeBodies.MaxBodyBytes)]
    [ProducesResponseType<AdminTradeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, _problemJson)]
    public async Task<IActionResult> Update([FromRoute] TradeKeyRoute route, [FromBody] UpdateTradeBody body, CancellationToken cancellationToken)
    {
        if (User.FindFirst("sub")?.Value is not { Length: > 0 } subject)
        {
            return Unauthorized();
        }

        if (body.Clave is not null && !string.Equals(body.Clave, route.Clave, StringComparison.Ordinal))
        {
            return KeyCannotChange();
        }

        try
        {
            return Ok(await service.UpdateAsync(subject, route.Clave!, body, cancellationToken));
        }
        catch (DomainException exception) when (exception.Code == TradeErrorCodes.AuditUnavailableCode)
        {
            return AuditUnavailable();
        }
    }

    private ObjectResult KeyCannotChange()
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status422UnprocessableEntity, "One or more validation errors occurred.");
        problem.Extensions["code"] = ErrorCodes.ValidationFailedCode;
        problem.Extensions["errors"] = new Dictionary<string, string[]> { ["clave"] = [ValidationFilter.GenericReason] };
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status422UnprocessableEntity, ContentTypes = { _problemJson } };
    }

    private ObjectResult AuditUnavailable()
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status503ServiceUnavailable, "Service Unavailable");
        problem.Extensions["code"] = TradeErrorCodes.AuditUnavailableCode;
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status503ServiceUnavailable, ContentTypes = { _problemJson } };
    }
}
