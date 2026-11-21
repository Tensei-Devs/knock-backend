using Asp.Versioning;
using Knock.Identity.Api.Modules.Trust.Dtos;
using Knock.Identity.Api.Modules.Trust.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Identity.Api.Modules.Trust.Controllers.V1;

/// <summary>Enviar a revisión la verificación propia (spec 025, R-13). Todas las fotos del nivel deben estar cifradas. Nada se cachea (R-18).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/me/verificacion/{id}/enviar")]
[Authorize]
internal sealed class EnvioVerificacionController(VerificationService verifications) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(CreateVerificationRequest.MaxBodyBytes)]
    [ProducesResponseType<VerificationSentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Submit(string id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var sub = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(sub))
        {
            return Unauthorized();
        }

        var sent = await verifications.SubmitAsync(sub, id, cancellationToken);
        return sent is null ? Unauthorized() : Ok(sent);
    }
}
