using Asp.Versioning;
using Knock.Market.Api.Modules.Requests.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Modules.Requests.Controllers.V1;

/// <summary>
/// Comparar y elegir cotización (spec 021, HU-033): el cliente dueño ve las cotizaciones de su solicitud y elige una, lo que crea la visita.
/// Ninguna respuesta se cachea (<c>Cache-Control: private, no-store</c>).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/solicitudes/{id}")]
[Authorize]
internal sealed class QuoteComparisonController : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>Cotizaciones vivas de una solicitud propia, con el orden pedido.</summary>
    [HttpGet("cotizaciones")]
    [ResponseCache(NoStore = true)]
    [ProducesResponseType<QuoteComparison>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public ActionResult<QuoteComparison> Compare(string id, [FromQuery] QuoteComparisonQuery query) => NotImplemented();

    /// <summary>Elige una cotización: la solicitud pasa a <c>cita_confirmada</c> y se crea la visita.</summary>
    [HttpPost("elegir")]
    [ResponseCache(NoStore = true)]
    [ProducesResponseType<Appointment>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, _problemJson)]
    public ActionResult<Appointment> Choose(string id, [FromBody] ChooseQuoteBody body) => NotImplemented();

    // Contrato primero (KNOCK-82, borrador): el servicio llega en los siguientes commits de la rama.
    private StatusCodeResult NotImplemented() => StatusCode(StatusCodes.Status501NotImplemented);
}
