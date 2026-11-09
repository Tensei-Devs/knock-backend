using Asp.Versioning;
using Knock.Identity.Api.Modules.Sesiones.Dtos;
using Knock.Identity.Api.Modules.Sesiones.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Identity.Api.Modules.Sesiones.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
internal sealed class LogoutController(LogoutService logout, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Cierra la sesión del dispositivo: la revoca y cancela su <c>sid</c> en Redis (spec 016). Sin cuerpo.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var sessionId = User.FindFirst("sid")?.Value;
        var sub = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(sub))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: "Invalid credentials.");
        }

        var remaining = TimeSpan.FromSeconds(1);
        if (long.TryParse(User.FindFirst("exp")?.Value, System.Globalization.CultureInfo.InvariantCulture, out var expiresAt))
        {
            remaining = DateTimeOffset.FromUnixTimeSeconds(expiresAt) - timeProvider.GetUtcNow();
        }

        await logout.LogoutAsync(sessionId, sub, remaining, cancellationToken);
        return NoContent();
    }
}
