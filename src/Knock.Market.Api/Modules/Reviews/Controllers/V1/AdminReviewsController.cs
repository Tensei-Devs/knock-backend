using Asp.Versioning;
using Knock.Hosting.Mvc;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Reviews.Dtos;
using Knock.Market.Api.Modules.Reviews.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MongoDB.Bson;

namespace Knock.Market.Api.Modules.Reviews.Controllers.V1;

/// <summary>
/// Ocultar y volver a mostrar una opinión desde el panel (spec 061). Exigen el token del panel y el permiso <c>reviews:moderate</c> (esquema <c>PanelBearer</c>, spec 045): sin sesión 401, sin permiso 403.
/// Nunca se borra nada. Nunca se cachea.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/opiniones/{id}")]
[Authorize(AuthenticationSchemes = PanelAuthentication.Scheme, Policy = PanelAuthentication.ReviewsModeratePolicy)]
[PrivateNoStore]
[EnableRateLimiting(MarketRateLimits.AdminReviewsPolicy)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
internal sealed class AdminReviewsController(ReviewModerationService service) : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>Oculta la opinión con un motivo de 8 a 300 caracteres. Repetirlo responde 200 sin cambios.</summary>
    [HttpPost("ocultar")]
    [RequestSizeLimit(HideReviewBody.MaxBodyBytes)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    public async Task<IActionResult> Hide(string id, [FromBody] HideReviewBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!ObjectId.TryParse(id, out var reviewId))
        {
            return BadRequest();
        }

        await service.HideAsync(User.FindFirst("sub")?.Value ?? string.Empty, reviewId, body.Motivo!, cancellationToken);
        return Ok();
    }

    /// <summary>Vuelve a mostrar la opinión. Sobre una visible responde 200 sin cambios.</summary>
    [HttpPost("mostrar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    public async Task<IActionResult> Show(string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var reviewId))
        {
            return BadRequest();
        }

        await service.ShowAsync(User.FindFirst("sub")?.Value ?? string.Empty, reviewId, cancellationToken);
        return Ok();
    }
}
