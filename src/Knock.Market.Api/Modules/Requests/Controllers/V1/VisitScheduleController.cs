using Asp.Versioning;
using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;
using Knock.Market.Api.Modules.Requests.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Market.Api.Modules.Requests.Controllers.V1;

/// <summary>
/// Agenda del técnico y cambios de la visita (spec 050, VIS-02 y VIS-04 a VIS-07). Recibe, valida el formato y delega en <see cref="VisitScheduleService"/>; no toca MongoDB. La
/// persona es siempre la del <c>sub</c> del token. Nunca se cachea (<c>Cache-Control: private, no-store</c>).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
[PrivateNoStore]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, ProblemJson)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, ProblemJson)]
internal sealed class VisitScheduleController(VisitScheduleService service, VisitCloseService closing) : BusinessRuleController
{
    /// <summary>VIS-02: agenda del técnico entre <c>desde</c> y <c>hasta</c> (por omisión hoy y 14 días), por fecha y hora, con cursor.</summary>
    [HttpGet("agenda")]
    [EnableRateLimiting(RateLimitingExtensions.AgendaPolicy)]
    [ProducesResponseType<AgendaResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Agenda([FromQuery] AgendaQuery query, CancellationToken cancellationToken) =>
        Reply(await service.AgendaAsync(Subject(), query, cancellationToken), VisitScheduleErrorCodes.VisitNotReschedulableCode, "hasta");

    /// <summary>VIS-04: una parte propone otra fecha para la visita.</summary>
    [HttpPost("visitas/{id}/reprogramar")]
    [RequestSizeLimit(RescheduleBody.MaxBodyBytes)]
    [EnableRateLimiting(RateLimitingExtensions.VisitChangePolicy)]
    [ProducesResponseType<RescheduleProposalResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reschedule(string id, [FromBody] RescheduleBody body, CancellationToken cancellationToken) =>
        Reply(await service.ProposeAsync(Subject(), id, body.FechaHora!.Value, cancellationToken), VisitScheduleErrorCodes.VisitNotReschedulableCode, "fechaHora");

    /// <summary>VIS-05: la otra parte confirma o rechaza la propuesta.</summary>
    [HttpPost("visitas/{id}/reprogramar/respuesta")]
    [RequestSizeLimit(RescheduleBody.MaxBodyBytes)]
    [EnableRateLimiting(RateLimitingExtensions.VisitChangePolicy)]
    [ProducesResponseType<RescheduleReplyResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reply(string id, [FromBody] RescheduleReplyBody body, CancellationToken cancellationToken) =>
        Reply(await service.ReplyAsync(Subject(), id, body.Acepta!.Value, cancellationToken), VisitScheduleErrorCodes.NoPendingProposalCode, "acepta");

    /// <summary>VIS-06: cancela con motivo obligatorio; con menos de 24 horas queda <c>tardia</c>.</summary>
    [HttpPost("visitas/{id}/cancelar")]
    [RequestSizeLimit(RescheduleBody.MaxBodyBytes)]
    [EnableRateLimiting(RateLimitingExtensions.VisitChangePolicy)]
    [ProducesResponseType<CancelVisitResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(string id, [FromBody] CancelVisitBody body, CancellationToken cancellationToken) =>
        Reply(await service.CancelAsync(Subject(), id, body.Motivo!, cancellationToken), VisitScheduleErrorCodes.VisitAlreadyClosedCode, "motivo");

    /// <summary>VIS-07: el técnico registra que el cliente no estaba, dentro de la ventana de la cita y sin penalizar.</summary>
    [HttpPost("visitas/{id}/cliente-ausente")]
    [EnableRateLimiting(RateLimitingExtensions.VisitChangePolicy)]
    [ProducesResponseType<ClientAbsentResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ClientAbsent(string id, CancellationToken cancellationToken) =>
        Reply(await service.ClientAbsentAsync(Subject(), id, cancellationToken), VisitScheduleErrorCodes.OutsideArrivalWindowCode, "id");

    /// <summary>VIS-09: el técnico termina la visita en curso con descripción y fotos; no hay confirmación del cliente.</summary>
    [HttpPost("visitas/{id}/terminar")]
    [RequestSizeLimit(RescheduleBody.MaxBodyBytes)]
    [EnableRateLimiting(RateLimitingExtensions.VisitChangePolicy)]
    [ProducesResponseType<FinishVisitResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Finish(string id, [FromBody] FinishVisitBody body, CancellationToken cancellationToken) =>
        Reply(await closing.FinishAsync(Subject(), id, body, cancellationToken), VisitScheduleErrorCodes.VisitNotInProgressCode, "fotos");

    /// <summary>VIS-10: el cliente reporta que no se terminó, una sola vez y hasta 7 días después.</summary>
    [HttpPost("visitas/{id}/no-terminado")]
    [RequestSizeLimit(RescheduleBody.MaxBodyBytes)]
    [EnableRateLimiting(RateLimitingExtensions.VisitChangePolicy)]
    [ProducesResponseType<NotFinishedResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> ReportNotFinished(string id, [FromBody] NotFinishedBody body, CancellationToken cancellationToken)
    {
        var outcome = await closing.ReportNotFinishedAsync(Subject(), id, body.Texto!, cancellationToken);
        return outcome.Status == VisitScheduleStatus.Done
            ? StatusCode(StatusCodes.Status201Created, outcome.Value)
            : Reply(outcome, VisitScheduleErrorCodes.ReportWindowClosedCode, "texto");
    }

    /// <summary>VIS-11: el cliente autoriza o no publicar las fotos del trabajo.</summary>
    [HttpPut("visitas/{id}/permiso-fotos")]
    [RequestSizeLimit(RescheduleBody.MaxBodyBytes)]
    [EnableRateLimiting(RateLimitingExtensions.VisitChangePolicy)]
    [ProducesResponseType<PhotoPermissionResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetPhotoPermission(string id, [FromBody] PhotoPermissionBody body, CancellationToken cancellationToken) =>
        Reply(await closing.SetPhotoPermissionAsync(Subject(), id, body.Permiso, cancellationToken), VisitScheduleErrorCodes.VisitNotInProgressCode, "permiso");

    private IActionResult Reply<T>(VisitScheduleOutcome<T> outcome, string conflictCode, string invalidField)
        where T : class => outcome.Status switch
        {
            VisitScheduleStatus.Done => Ok(outcome.Value),
            VisitScheduleStatus.NotATechnician => Rule(StatusCodes.Status403Forbidden, ErrorCodes.NotATechnicianCode),
            VisitScheduleStatus.NotFound => NotFound(),
            VisitScheduleStatus.InvalidProposal => Invalid(invalidField),
            VisitScheduleStatus.NotReschedulable => Rule(StatusCodes.Status409Conflict, VisitScheduleErrorCodes.VisitNotReschedulableCode),
            VisitScheduleStatus.NoPendingProposal => Rule(StatusCodes.Status409Conflict, VisitScheduleErrorCodes.NoPendingProposalCode),
            VisitScheduleStatus.AlreadyClosed => Rule(StatusCodes.Status409Conflict, VisitScheduleErrorCodes.VisitAlreadyClosedCode),
            VisitScheduleStatus.NotInProgress => Rule(StatusCodes.Status409Conflict, VisitScheduleErrorCodes.VisitNotInProgressCode),
            VisitScheduleStatus.ReportWindowClosed => Rule(StatusCodes.Status409Conflict, VisitScheduleErrorCodes.ReportWindowClosedCode),
            _ => Rule(StatusCodes.Status409Conflict, conflictCode),
        };

    private ObjectResult Invalid(string field)
    {
        var result = Rule(StatusCodes.Status422UnprocessableEntity, ErrorCodes.ValidationFailedCode);
        ((ProblemDetails)result.Value!).Extensions["errors"] = new Dictionary<string, string[]> { [field] = ["The value is not valid."] };
        return result;
    }
}
