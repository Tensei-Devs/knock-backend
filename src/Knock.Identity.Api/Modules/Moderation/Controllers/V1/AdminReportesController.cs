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
/// Cola y decisión de reportes desde el panel (spec 053, REP-04 y REP-05). Exige el permiso <c>reports:resolve</c> con la sesión del panel (spec 033). La persona que decide es siempre la del
/// <c>sub</c> del token (R-23); nunca se cachea (R-21).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/reportes")]
[RequierePermiso(PanelApiPermissions.ReportsResolve)]
internal sealed class AdminReportesController(ReportReviewService service) : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>REP-04: la cola por gravedad (crítica primero) y antigüedad, con cursor. Muestra motivo y texto, nunca teléfono ni dirección.</summary>
    [HttpGet]
    [EnableRateLimiting(IdentityRateLimits.PanelReadPolicy)]
    [ProducesResponseType<ReportQueuePage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<ActionResult<ReportQueuePage>> List([FromQuery] ReportQueueQuery query, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return await service.ListAsync(query, cancellationToken);
    }

    /// <summary>REP-05: asigna la gravedad con motivo; la respuesta se deriva de ella. 404 <c>report_not_found</c>; 409 <c>report_already_decided</c>; 503 <c>audit_unavailable</c>.</summary>
    [HttpPost("{id}/decision")]
    [EnableRateLimiting(IdentityRateLimits.PanelDecisionPolicy)]
    [RequestSizeLimit(ReportDecisionBody.MaxBodyBytes)]
    [ProducesResponseType<ReportDecisionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, _problemJson)]
    public async Task<ActionResult<ReportDecisionResult>> Decide([FromRoute] ReviewRoute route, [FromBody] ReportDecisionBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        Response.Headers.CacheControl = "private, no-store";
        var actor = new ReviewActor(User.FindFirst("sub")?.Value ?? string.Empty, User.FindFirst("sid")?.Value);
        return await service.DecideAsync(actor, route.Id!, body, cancellationToken);
    }
}
