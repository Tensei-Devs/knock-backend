using Asp.Versioning;
using Knock.Identity.Api.Modules.Sesiones.Dtos;
using Knock.Identity.Api.Modules.Sesiones.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Identity.Api.Modules.Sesiones.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
internal sealed class RefreshController(SessionRefreshService refresh) : ControllerBase
{
    /// <summary>Límite del cuerpo de la renovación: 1 KiB (solo lleva el token de 43 caracteres).</summary>
    public const int MaxBodyBytes = 1024;

    /// <summary>Cambia un token de renovación por un par nuevo (spec 016). Cualquier rechazo responde el mismo 401 sin motivo.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [RequestSizeLimit(MaxBodyBytes)]
    [ProducesResponseType<RefreshResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await refresh.RefreshAsync(request.RefreshToken ?? string.Empty, cancellationToken);
        return result is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: "Invalid credentials.")
            : Ok(new RefreshResponse(result.AccessToken, result.ExpiresInSeconds, result.RefreshToken));
    }
}
