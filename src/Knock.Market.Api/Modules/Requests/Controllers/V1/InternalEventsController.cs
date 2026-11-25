using Asp.Versioning;
using Knock.Hosting.Mvc;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;
using Knock.Market.Api.Modules.Requests.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Modules.Requests.Controllers.V1;

/// <summary>
/// Ruta interna de eventos de Identidad (spec 054, R-15; spec 059, R-10 y R-11): solo la llama el procesador de la bandeja de Identidad con su token de servicio y el gateway no la enruta.
/// Responde 202 también si ya recibió ese <c>eventoId</c> (entrega al menos una vez, R-19). Nunca se cachea.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/interno/eventos")]
[Authorize(Policy = InternalServiceAuthentication.EventsPolicy)]
[PrivateNoStore]
internal sealed class InternalEventsController(AccountEventService service) : BusinessRuleController
{
    public const string EventConflictCode = "event_conflict";

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ProblemJson)]
    public async Task<IActionResult> Receive([FromBody] AccountEventBody body, CancellationToken cancellationToken) =>
        await service.ReceiveAsync(body, cancellationToken) == AccountEventOutcome.Conflict
            ? Rule(StatusCodes.Status503ServiceUnavailable, EventConflictCode)
            : Accepted();
}
