using Asp.Versioning;
using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Tecnicos.Domain;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using Knock.Market.Api.Modules.Tecnicos.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Market.Api.Modules.Tecnicos.Controllers.V1;

/// <summary>
/// Servicios y precios de referencia del técnico (spec 047, SRV-01 a SRV-04). Recibe, valida el formato y delega en <see cref="ServiceService"/>. La persona es la del <c>sub</c> del token (R-18).
/// Nunca se cachea en el cliente (<c>Cache-Control: private, no-store</c>).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tecnicos")]
[Authorize]
[PrivateNoStore]
[EnableRateLimiting(RateLimitingExtensions.ServicesPolicy)]
internal sealed class ServiciosController(ServiceService service) : ControllerBase
{
    public const string ServiceNotFoundCode = "service_not_found";
    public const string ServiceLimitReachedCode = "service_limit_reached";
    private const string _problemJson = TechnicianProblems.ProblemJson;

    /// <summary>Servicios y precios de referencia de un técnico visible (SRV-01), para cualquier persona con sesión.</summary>
    [HttpGet("{id}/servicios")]
    [ProducesResponseType<TechnicianServiceList>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<IActionResult> List([FromRoute] TechnicianServicesRoute route, CancellationToken cancellationToken) =>
        await service.ListAsync(route.Id!, cancellationToken) is { } list
            ? Ok(list)
            : TechnicianProblems.Rule(this, StatusCodes.Status404NotFound, TechnicianRules.TechnicianNotFoundCode);

    /// <summary>Agrega un servicio (SRV-02): 201; el 51.º responde 409 <c>service_limit_reached</c>.</summary>
    [HttpPost("me/servicios")]
    [RequestSizeLimit(CreateServiceBody.MaxBodyBytes)]
    [ProducesResponseType<TechnicianService>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<IActionResult> Create([FromBody] CreateServiceBody body, CancellationToken cancellationToken)
    {
        var outcome = await service.CreateAsync(Subject(), body, cancellationToken);
        return outcome.Status == ServiceStatus.Ok
            ? Created(new Uri($"/api/v1/tecnicos/me/servicios/{outcome.Service!.Id}", UriKind.Relative), outcome.Service)
            : Fail(outcome);
    }

    /// <summary>Edita un servicio propio (SRV-03): cambian solo los campos enviados.</summary>
    [HttpPut("me/servicios/{id}")]
    [RequestSizeLimit(UpdateServiceBody.MaxBodyBytes)]
    [ProducesResponseType<TechnicianService>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<IActionResult> Update([FromRoute] ServiceRoute route, [FromBody] UpdateServiceBody body, CancellationToken cancellationToken)
    {
        var outcome = await service.UpdateAsync(Subject(), route.Id!, body, cancellationToken);
        return outcome.Status == ServiceStatus.Ok ? Ok(outcome.Service) : Fail(outcome);
    }

    /// <summary>Quita un servicio propio (SRV-04): 204; uno ajeno o inexistente responde 404 <c>service_not_found</c>.</summary>
    [HttpDelete("me/servicios/{id}")]
    [RequestSizeLimit(1024)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<IActionResult> Delete([FromRoute] ServiceRoute route, CancellationToken cancellationToken)
    {
        var outcome = await service.DeleteAsync(Subject(), route.Id!, cancellationToken);
        return outcome.Status == ServiceStatus.Ok ? NoContent() : Fail(outcome);
    }

    private string Subject() => User.FindFirst("sub")?.Value ?? string.Empty;

    private ObjectResult Fail(ServiceOutcome outcome) => outcome.Status switch
    {
        ServiceStatus.NotATechnician => TechnicianProblems.Rule(this, StatusCodes.Status404NotFound, ErrorCodes.NotATechnicianCode),
        ServiceStatus.TradeInvalid => TechnicianProblems.Rule(this, StatusCodes.Status422UnprocessableEntity, ErrorCodes.TradeInvalidCode, "oficio"),
        ServiceStatus.LimitReached => TechnicianProblems.Rule(this, StatusCodes.Status409Conflict, ServiceLimitReachedCode),
        _ => TechnicianProblems.Rule(this, StatusCodes.Status404NotFound, ServiceNotFoundCode),
    };
}
