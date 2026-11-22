using Asp.Versioning;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Assistant.Dtos;
using Knock.Market.Api.Modules.Assistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace Knock.Market.Api.Modules.Assistant.Controllers.V1;

/// <summary>
/// Asistente de IA (spec 052, ASI-01 a ASI-06). Recibe, valida el formato y delega; la persona es siempre la del <c>sub</c> del token (R-23). Ninguna respuesta se cachea del lado del cliente: son datos
/// propios (<c>Cache-Control: private, no-store</c>). La salida de la IA es una sugerencia que la persona revisa (R-22).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/asistente")]
[Authorize]
internal sealed class AsistenteController(AsistenteService assistant, RangoPrecioService prices, BitacoraService logs) : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>ASI-01: un turno con el asistente Knock (cliente o técnico).</summary>
    [HttpPost("conversaciones")]
    [RequestSizeLimit(TurnoBody.MaxBodyBytes)]
    [EnableRateLimiting(MarketRateLimits.AssistantAiPolicy)]
    [ProducesResponseType<TurnoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, _problemJson)]
    public async Task<IActionResult> Turn([FromBody] TurnoBody body, CancellationToken cancellationToken) =>
        Respond(await assistant.TurnAsync(Subject(), body, cancellationToken));

    /// <summary>ASI-02: resumen de una conversación en la que el técnico participa.</summary>
    [HttpPost("resumen-chat")]
    [RequestSizeLimit(ConversacionBody.MaxBodyBytes)]
    [EnableRateLimiting(MarketRateLimits.AssistantAiPolicy)]
    [ProducesResponseType<ResumenChatResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<IActionResult> Summarize([FromBody] ConversacionBody body, CancellationToken cancellationToken) =>
        Respond(await assistant.SummarizeAsync(Subject(), body.ConversacionId!, cancellationToken));

    /// <summary>ASI-03: borrador editable de cotización; nunca se envía ni se guarda como cotización.</summary>
    [HttpPost("borrador-cotizacion")]
    [RequestSizeLimit(ConversacionBody.MaxBodyBytes)]
    [EnableRateLimiting(MarketRateLimits.AssistantAiPolicy)]
    [ProducesResponseType<BorradorCotizacionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<IActionResult> DraftQuote([FromBody] ConversacionBody body, CancellationToken cancellationToken) =>
        Respond(await assistant.DraftQuoteAsync(Subject(), body.ConversacionId!, cancellationToken));

    /// <summary>ASI-04: rango de precio de la referencia de mercado (spec 048), en caché de 1 hora.</summary>
    [HttpGet("rango-precio")]
    [EnableRateLimiting(MarketRateLimits.AssistantReadPolicy)]
    [ProducesResponseType<RangoPrecioResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    public async Task<IActionResult> PriceRange([FromQuery] RangoPrecioQuery query, CancellationToken cancellationToken) =>
        Respond(await prices.GetAsync(Subject(), query.Oficio!, query.TipoTrabajo!, cancellationToken));

    /// <summary>ASI-05: guarda o reemplaza la bitácora del técnico (201 al crear, 200 al reemplazar).</summary>
    [HttpPut("bitacora/{solicitudId}")]
    [RequestSizeLimit(BitacoraBody.MaxBodyBytes)]
    [EnableRateLimiting(MarketRateLimits.AssistantReadPolicy)]
    [ProducesResponseType<BitacoraResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<BitacoraResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    public async Task<IActionResult> SaveLog([FromRoute] SolicitudRoute route, [FromBody] BitacoraBody body, CancellationToken cancellationToken)
    {
        var outcome = await logs.SaveAsync(Subject(), route.SolicitudId!, body, cancellationToken);
        return outcome.Status == AssistantStatus.Created ? Created(new Uri($"/api/v1/asistente/bitacora/{route.SolicitudId}", UriKind.Relative), outcome.Value) : Respond(outcome);
    }

    /// <summary>ASI-06: lee la bitácora propia; la de otro técnico responde 404 <c>log_not_found</c>.</summary>
    [HttpGet("bitacora/{solicitudId}")]
    [EnableRateLimiting(MarketRateLimits.AssistantReadPolicy)]
    [ProducesResponseType<BitacoraResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    public async Task<IActionResult> GetLog([FromRoute] SolicitudRoute route, CancellationToken cancellationToken) =>
        Respond(await logs.GetAsync(Subject(), route.SolicitudId!, cancellationToken));

    private string Subject() => User.FindFirst("sub")?.Value ?? string.Empty;

    private ObjectResult Respond<T>(AssistantOutcome<T> outcome) where T : class
    {
        Response.Headers.CacheControl = "private, no-store";
        return outcome.Status switch
        {
            AssistantStatus.Ok or AssistantStatus.Created => Ok(outcome.Value),
            AssistantStatus.NotTechnician => Problem(StatusCodes.Status403Forbidden, "not_a_technician"),
            AssistantStatus.Forbidden => Problem(StatusCodes.Status403Forbidden, "forbidden"),
            AssistantStatus.NotFound => Problem(StatusCodes.Status404NotFound, "log_not_found"),
            AssistantStatus.DailyLimit => Problem(StatusCodes.Status429TooManyRequests, "ai_daily_limit_reached", outcome.RetryAfterSeconds),
            AssistantStatus.ReferenceUnavailable => Problem(StatusCodes.Status503ServiceUnavailable, "price_reference_unavailable", 60),
            _ => Problem(StatusCodes.Status503ServiceUnavailable, "ai_unavailable", 30),
        };
    }

    private ObjectResult Problem(int status, string code, int retryAfterSeconds = 0)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, status, ReasonPhrases.GetReasonPhrase(status));
        problem.Extensions["code"] = code;
        if (retryAfterSeconds > 0)
        {
            Response.Headers.RetryAfter = retryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { _problemJson } };
    }
}
