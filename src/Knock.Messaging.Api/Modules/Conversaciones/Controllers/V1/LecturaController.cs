using Asp.Versioning;
using Knock.Contracts.Pagination;
using Knock.Hosting.Mvc;
using Knock.Messaging.Api.Hosting;
using Knock.Messaging.Api.Modules.Conversaciones.Dtos;
using Knock.Messaging.Api.Modules.Conversaciones.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Messaging.Api.Modules.Conversaciones.Controllers.V1;

/// <summary>
/// Lectura de una conversación (spec 030, R-16 y R-19): el historial de mensajes paginado y «marcar como leído». Recibe, valida el formato y delega en <see cref="LecturaService"/>; no toca
/// MongoDB, Redis ni HTTP. Nunca se cachea (<c>Cache-Control: private, no-store</c>).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/conversaciones/{id}")]
[Authorize]
[PrivateNoStore]
internal sealed class LecturaController(LecturaService service) : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>Los mensajes de la conversación del más reciente al más antiguo, con paginación por cursor (<c>limit</c> de 1 a 50, 30 por omisión).</summary>
    [HttpGet("mensajes")]
    [ProducesResponseType<CursorPage<MensajeView>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public Task<CursorPage<MensajeView>> Messages(string id, [FromQuery] MensajesQuery query, CancellationToken cancellationToken) =>
        service.MessagesAsync(CallerSession.Subject(User), id, query, cancellationToken);

    /// <summary>Marca como leído hasta <c>hasta</c> (acotado al último mensaje). Repetirlo o mandar una fecha anterior responde 204 sin cambios.</summary>
    [HttpPost("leido")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<IActionResult> MarkRead(string id, LeidoBody body, CancellationToken cancellationToken)
    {
        await service.MarkReadAsync(CallerSession.Subject(User), id, body.Hasta!.Value, cancellationToken);
        return NoContent();
    }
}
