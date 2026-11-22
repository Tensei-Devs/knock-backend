using Asp.Versioning;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Requests.Dtos;
using Knock.Market.Api.Modules.Requests.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Modules.Requests.Controllers.V1;

/// <summary>
/// Ruta interna que entrega a Identidad el texto del reporte de «no terminado» (spec 053; spec 050, R-32): <c>ReporteNoTerminadoCreado</c> viaja solo con identificadores y Identidad lo lee aquí. La llama
/// únicamente Identidad con su token de servicio (<see cref="InternalServiceAuthentication"/>, permiso <c>reporte-no-terminado</c>) y el gateway no la enruta. Además de la firma, el token lleva el
/// <c>visitaId</c> pedido y debe coincidir con la ruta: un token capturado no sirve para leer otro reporte. Nunca se cachea.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/interno/visitas/{visitaId}/reporte-no-terminado")]
[Authorize(Policy = InternalServiceAuthentication.NotFinishedReportPolicy)]
[PrivateNoStore]
internal sealed class InternalNotFinishedReportController(NotFinishedReportReadService service) : BusinessRuleController
{
    public const string VisitClaim = "visitaId";
    public const string NotFoundCode = "not_finished_report_not_found";

    [HttpGet]
    [ProducesResponseType<NotFinishedReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, ProblemJson)]
    public async Task<IActionResult> Get(string visitaId, CancellationToken cancellationToken)
    {
        if (!string.Equals(User.FindFirst(VisitClaim)?.Value, visitaId, StringComparison.OrdinalIgnoreCase))
        {
            return Rule(StatusCodes.Status403Forbidden, InternalServiceAuthentication.ServiceNotAllowedCode);
        }

        return await service.FindAsync(visitaId, cancellationToken) is { } report ? Ok(report) : Rule(StatusCodes.Status404NotFound, NotFoundCode);
    }
}
