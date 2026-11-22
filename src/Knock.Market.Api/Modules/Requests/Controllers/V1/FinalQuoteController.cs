using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;
using Knock.Market.Api.Modules.Requests.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Market.Api.Modules.Requests.Controllers.V1;

/// <summary>
/// Cotización final tras la visita (spec 049, COT-06 y COT-07) y ajustes de precio (AJU-01 y AJU-02). Recibe, valida el formato y delega en <see cref="FinalQuoteService"/>;
/// no toca MongoDB. La persona es siempre la del <c>sub</c> del token. Nunca se cachea (<c>Cache-Control: private, no-store</c>).
/// </summary>
[Route("api/v{version:apiVersion}")]
internal sealed class FinalQuoteController(FinalQuoteService service) : DocumentedRequestsController
{
    /// <summary>COT-06: el técnico de la visita envía la cotización final (una sola por visita, con la visita en curso).</summary>
    [HttpPost("visitas/{id}/cotizacion-final")]
    [RequestSizeLimit(FinalQuoteBody.MaxBodyBytes)]
    [EnableRateLimiting(MarketRateLimits.FinalQuotePolicy)]
    [ProducesResponseType<FinalQuoteResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> SendFinalQuote(string id, [FromBody] FinalQuoteBody body, CancellationToken cancellationToken) =>
        Reply(await service.SendFinalQuoteAsync(Subject(), id, body.Precio!.Value, body.Descripcion!, cancellationToken), StatusCodes.Status201Created, FinalQuoteErrorCodes.FinalQuoteNotAllowedCode);

    /// <summary>COT-07: el cliente dueño aprueba o rechaza la cotización final. La decisión es definitiva.</summary>
    [HttpPost("visitas/{id}/cotizacion-final/decision")]
    [RequestSizeLimit(FinalQuoteBody.MaxBodyBytes)]
    [EnableRateLimiting(MarketRateLimits.FinalQuoteDecisionPolicy)]
    [ProducesResponseType<FinalQuoteDecisionResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DecideFinalQuote(string id, [FromBody] DecisionBody body, CancellationToken cancellationToken) =>
        Reply(await service.DecideFinalQuoteAsync(Subject(), id, body.Decision!, cancellationToken), StatusCodes.Status200OK, FinalQuoteErrorCodes.FinalQuoteAlreadyDecidedCode);

    /// <summary>AJU-01: el técnico de una cotización aceptada propone un monto mayor al tope, con motivo (un solo ajuste pendiente por cotización).</summary>
    [HttpPost("cotizaciones/{id}/ajuste-precio")]
    [RequestSizeLimit(FinalQuoteBody.MaxBodyBytes)]
    [EnableRateLimiting(MarketRateLimits.PriceAdjustmentPolicy)]
    [ProducesResponseType<AdjustmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ProblemJson)]
    public async Task<IActionResult> ProposeAdjustment(string id, [FromBody] AdjustmentBody body, CancellationToken cancellationToken) =>
        Reply(await service.ProposeAdjustmentAsync(Subject(), id, body.MontoNuevo!.Value, body.Motivo!, cancellationToken), StatusCodes.Status201Created, FinalQuoteErrorCodes.AdjustmentNotAllowedCode);

    /// <summary>AJU-02: el cliente dueño aprueba o rechaza el ajuste; el precio cambia solo si aprueba.</summary>
    [HttpPost("ajustes/{id}/decision")]
    [RequestSizeLimit(FinalQuoteBody.MaxBodyBytes)]
    [EnableRateLimiting(MarketRateLimits.AdjustmentDecisionPolicy)]
    [ProducesResponseType<AdjustmentDecisionResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DecideAdjustment(string id, [FromBody] DecisionBody body, CancellationToken cancellationToken) =>
        Reply(await service.DecideAdjustmentAsync(Subject(), id, body.Decision!, cancellationToken), StatusCodes.Status200OK, FinalQuoteErrorCodes.AdjustmentAlreadyDecidedCode);

    private IActionResult Reply<T>(FinalQuoteOutcome<T> outcome, int successStatus, string conflictCode)
        where T : class =>
        outcome.Status == FinalQuoteStatus.Done ? StatusCode(successStatus, outcome.Value) : Failure(outcome.Status, conflictCode);

    /// <summary>Respuesta de cada caso fijo. <paramref name="conflictCode"/> es el <c>code</c> del 409 de la ruta (el «no se puede» o el «ya se decidió»).</summary>
    private IActionResult Failure(FinalQuoteStatus status, string conflictCode) => status switch
    {
        FinalQuoteStatus.NotATechnician => Rule(StatusCodes.Status403Forbidden, ErrorCodes.NotATechnicianCode),
        FinalQuoteStatus.NotTheOwner => Rule(StatusCodes.Status403Forbidden, VisitAccessCodes.NotTheOwnerCode),
        FinalQuoteStatus.NotFound => NotFound(),
        FinalQuoteStatus.AmountTooLow => AmountTooLow(),
        FinalQuoteStatus.ContentRejected => Rule(StatusCodes.Status422UnprocessableEntity, RequestErrorCodes.ContentRejectedCode),
        FinalQuoteStatus.ModerationUnavailable => Rule(StatusCodes.Status503ServiceUnavailable, RequestErrorCodes.ModerationUnavailableCode),
        _ => Rule(StatusCodes.Status409Conflict, conflictCode),
    };

    private ObjectResult AmountTooLow()
    {
        var result = Rule(StatusCodes.Status422UnprocessableEntity, ErrorCodes.ValidationFailedCode);
        ((ProblemDetails)result.Value!).Extensions["errors"] = new Dictionary<string, string[]> { ["montoNuevo"] = ["The value is not valid."] };
        return result;
    }
}
