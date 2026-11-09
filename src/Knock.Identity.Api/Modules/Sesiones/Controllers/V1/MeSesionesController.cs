using Asp.Versioning;
using Knock.Identity.Api.Modules.Sesiones.Dtos;
using Knock.Identity.Api.Modules.Sesiones.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace Knock.Identity.Api.Modules.Sesiones.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/me/sesiones")]
[Authorize]
internal sealed class MeSesionesController(SessionManagementService sessions) : ControllerBase
{
    /// <summary>Lista las sesiones no revocadas ni vencidas de la cuenta autenticada, sin exponer tokens ni huellas.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SessionListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (!TryGetSessionIdentity(out var sub, out var currentSessionId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized");
        }

        return Ok(await sessions.ListOpenSessionsAsync(sub, currentSessionId, cancellationToken));
    }

    /// <summary>Revoca una sesión propia distinta de la actual y bloquea su <c>sid</c> en Redis hasta que venza el JWT.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> Revoke(string id, CancellationToken cancellationToken)
    {
        if (!TryGetSessionIdentity(out var sub, out var currentSessionId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized");
        }

        await sessions.RevokeSessionAsync(sub, currentSessionId, id, cancellationToken);
        return NoContent();
    }

    private bool TryGetSessionIdentity(out string sub, out string sessionId)
    {
        sub = User.FindFirst("sub")?.Value ?? string.Empty;
        sessionId = User.FindFirst("sid")?.Value ?? string.Empty;
        return Guid.TryParse(sub, out _) && ObjectId.TryParse(sessionId, out _);
    }
}
