using Asp.Versioning;
using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Modules.Moderation.Dtos;
using Knock.Identity.Api.Modules.Moderation.Services;
using Knock.Identity.Api.Modules.Panel;
using Knock.Identity.Api.Modules.Panel.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Identity.Api.Modules.Moderation.Controllers.V1;

/// <summary>
/// Revisión de verificaciones y recuperaciones de acceso desde el panel (spec 053, VER-03 a VER-05). Todas las rutas exigen el permiso <c>verification:review</c> con la sesión del panel (spec 033):
/// la persona que actúa es siempre la del <c>sub</c> del token (R-23) y ninguna respuesta se guarda en caché (R-21). Recibe, valida el formato y delega en el servicio; no toca MongoDB.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/verificaciones")]
[RequierePermiso(PanelApiPermissions.VerificationReview)]
internal sealed class AdminVerificacionesController(VerificationReviewService service) : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>VER-03: la cola, de la más antigua a la más reciente, con cursor. Sin fotos ni datos personales.</summary>
    [HttpGet]
    [EnableRateLimiting(IdentityRateLimits.PanelReadPolicy)]
    [ProducesResponseType<VerificationQueuePage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<ActionResult<VerificationQueuePage>> List([FromQuery] VerificationQueueQuery query, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return await service.ListAsync(query, cancellationToken);
    }

    /// <summary>VER-04: abre una verificación; entrega enlaces de 5 minutos a las fotos y registra quién los abrió. 404 <c>verification_not_found</c>; 410 <c>photos_destroyed</c>.</summary>
    [HttpGet("{id}")]
    [EnableRateLimiting(IdentityRateLimits.PanelDecisionPolicy)]
    [ProducesResponseType<VerificationDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, _problemJson)]
    public async Task<ActionResult<VerificationDetail>> Open([FromRoute] ReviewRoute route, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return await service.OpenAsync(Actor(), route.Id!, cancellationToken);
    }

    /// <summary>VER-05: aprueba o rechaza con motivo; destruye la llave de las fotos (identidad) o cambia el número (recuperación). 404, 409 <c>verification_already_decided</c>, 503 <c>audit_unavailable</c>.</summary>
    [HttpPost("{id}/decision")]
    [EnableRateLimiting(IdentityRateLimits.PanelDecisionPolicy)]
    [RequestSizeLimit(VerificationDecisionBody.MaxBodyBytes)]
    [ProducesResponseType<VerificationDecisionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, _problemJson)]
    public async Task<ActionResult<VerificationDecisionResult>> Decide([FromRoute] ReviewRoute route, [FromBody] VerificationDecisionBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        Response.Headers.CacheControl = "private, no-store";
        return await service.DecideAsync(Actor(), route.Id!, body, cancellationToken);
    }

    private ReviewActor Actor() => new(User.FindFirst("sub")?.Value ?? string.Empty, User.FindFirst("sid")?.Value);
}
