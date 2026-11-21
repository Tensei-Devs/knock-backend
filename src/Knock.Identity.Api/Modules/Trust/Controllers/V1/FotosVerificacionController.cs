using Asp.Versioning;
using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Modules.Trust.Dtos;
using Knock.Identity.Api.Modules.Trust.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Identity.Api.Modules.Trust.Controllers.V1;

/// <summary>
/// Confirmar una foto de la verificación propia (spec 025, R-10 y R-12). El servidor descarga la cuarentena, valida el contenido, cifra y guarda; la app solo sube con el
/// enlace firmado. La ruta no lleva identificador de persona. Nada se cachea (R-18).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/me/verificacion/{id}/fotos/{tipo}")]
[Authorize]
internal sealed class FotosVerificacionController(VerificationService verifications) : ControllerBase
{
    [HttpPost("confirmar")]
    [RequestSizeLimit(CreateVerificationRequest.MaxBodyBytes)]
    [EnableRateLimiting(RateLimitingExtensions.VerificationConfirmPolicy)]
    [ProducesResponseType<PhotoConfirmedResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Confirm(string id, string tipo, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var sub = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(sub))
        {
            return Unauthorized();
        }

        var confirmed = await verifications.ConfirmPhotoAsync(sub, id, tipo, cancellationToken);
        return confirmed is null ? Unauthorized() : Ok(confirmed);
    }
}
