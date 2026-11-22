using Asp.Versioning;
using Knock.Messaging.Api.Hosting;
using Knock.Messaging.Api.Modules.Conversaciones.Dtos;
using Knock.Messaging.Api.Modules.Conversaciones.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Messaging.Api.Modules.Conversaciones.Controllers.V1;

/// <summary>
/// Envío de mensajes de una conversación (spec 030, R-08). Recibe, delega en <see cref="EnvioDeMensajeService"/> y responde; no toca MongoDB, Redis ni HTTP. El límite de 30 por minuto por
/// persona lo cobra el limitador antes de la acción y los errores de dominio los traduce el manejador del módulo.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/conversaciones/{id}/mensajes")]
[Authorize]
[PrivateNoStore]
internal sealed class MensajesController(EnvioDeMensajeService envio) : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>Envía un mensaje: oculta datos de contacto antes de elegir, lo modera y lo guarda; luego lo entrega en vivo.</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitingExtensions.SendMessagePolicy)]
    [RequestSizeLimit(4096)]
    [ProducesResponseType<MensajeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, _problemJson)]
    public async Task<ActionResult<MensajeResponse>> Send(string id, EnviarMensajeRequest body, CancellationToken cancellationToken)
    {
        var token = Request.Headers.Authorization.ToString()["Bearer ".Length..];
        var message = await envio.EnviarAsync(User.FindFirst("sub")?.Value ?? string.Empty, token, id, body.Texto!, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, message);
    }
}
