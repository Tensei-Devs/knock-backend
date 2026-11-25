using Asp.Versioning;
using Knock.Hosting.Mvc;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;
using Knock.Market.Api.Modules.Requests.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MongoDB.Bson;

namespace Knock.Market.Api.Modules.Requests.Controllers.V1;

/// <summary>
/// Cancelar una solicitud propia (spec 029, HU-027): la cascada (solicitud, cotizaciones, envíos y evento) ocurre en una sola transacción en
/// <see cref="CierreDeSolicitudService"/>. Recibe, valida el formato y delega; no toca MongoDB. Nunca se cachea (<c>Cache-Control: private, no-store</c>).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/solicitudes/{id}/cancelar")]
[Authorize]
[PrivateNoStore]
internal sealed class CancelacionController(CierreDeSolicitudService service) : BusinessRuleController
{
    /// <summary>Cancela la solicitud del cliente mientras no tenga cita. Es idempotente: repetirla devuelve el mismo cuerpo sin otro evento.</summary>
    [HttpPost]
    [RequestSizeLimit(CancelRequestBody.MaxBodyBytes)]
    [EnableRateLimiting(MarketRateLimits.CancelRequestPolicy)]
    [ProducesResponseType<CancelRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, ProblemJson)]
    public async Task<IActionResult> Cancel(string id, [FromBody] CancelRequestBody? body, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var requestId))
        {
            return BadRequest();
        }

        var result = await service.CancelAsync(requestId, Subject(), body?.Motivo, cancellationToken);
        return result.Status switch
        {
            "cancelada" => Ok(new CancelRequestResponse(result.Id, "cancelada", new DateTimeOffset(result.CancelledAt!.Value, TimeSpan.Zero), result.Reason)),
            "request_expired" => Rule(StatusCodes.Status409Conflict, RequestErrorCodes.RequestExpiredCode),
            "request_with_appointment" or "unchanged" => Rule(StatusCodes.Status409Conflict, RequestErrorCodes.RequestWithAppointmentCode),
            _ => NotFound(),
        };
    }
}
