using Asp.Versioning;
using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Modules.Safety.Dtos;
using Knock.Identity.Api.Modules.Safety.Services;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Identity.Api.Modules.Safety.Controllers.V1;

/// <summary>
/// Entrega interna de <c>ReporteNoTerminadoCreado</c> de Mercado (spec 053; spec 050, R-32): solo la llama Mercado con su token de servicio y el permiso <c>reporte-no-terminado-creado</c>
/// (<see cref="InternalServiceTokenAttribute"/>); el gateway no la enruta. El evento lleva solo identificadores: <see cref="NotFinishedReportConsumer"/> lee el texto en Mercado y abre el reporte en la cola
/// del panel. 202 si lo abrió o ya existía; 503 si Mercado no responde, para que quien entrega reintente. Nunca se cachea.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/interno/reportes-no-terminados")]
[InternalServiceToken(Permission)]
internal sealed class InternalNotFinishedReportsController(NotFinishedReportConsumer consumer) : ControllerBase
{
    public const string Permission = "reporte-no-terminado-creado";
    private const string _problemJson = "application/problem+json";

    [HttpPost]
    [RequestSizeLimit(2048)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, _problemJson)]
    public async Task<IActionResult> Post([FromBody] NotFinishedReportEventBody body, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        await consumer.ConsumeAsync(body, cancellationToken);
        return Accepted();
    }
}
