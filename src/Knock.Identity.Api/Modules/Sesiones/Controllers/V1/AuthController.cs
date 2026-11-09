using Asp.Versioning;
using Knock.Identity.Api.Modules.Sesiones.Dtos;
using Knock.Identity.Api.Modules.Sesiones.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Identity.Api.Modules.Sesiones.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
internal sealed class AuthController(LoginService login) : ControllerBase
{
    /// <summary>Límite del cuerpo del canje: 8 KiB.</summary>
    public const int MaxBodyBytes = 8192;

    /// <summary>Registro e inicio de sesión en un solo endpoint (spec 015): 201 con cuenta nueva, 200 con cuenta existente.</summary>
    [HttpPost("firebase")]
    [AllowAnonymous]
    [RequestSizeLimit(MaxBodyBytes)]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ExchangeFirebaseToken([FromBody] FirebaseExchangeRequest request, CancellationToken cancellationToken)
    {
        // El ValidationFilter ya garantizó que los cuatro campos vienen y son válidos.
        var result = await login.LoginAsync(
            new LoginRequest(request.FirebaseIdToken ?? string.Empty, request.DeviceId ?? string.Empty, request.Dispositivo ?? string.Empty),
            cancellationToken);
        if (result is null)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: "Invalid credentials.");
        }

        var body = new SessionResponse(result.Session.AccessToken, result.Session.RefreshToken, result.IsNew, result.ProfileComplete);
        return result.IsNew ? StatusCode(StatusCodes.Status201Created, body) : Ok(body);
    }
}
