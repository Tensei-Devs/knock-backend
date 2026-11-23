using Asp.Versioning;
using Knock.Identity.Api.Modules.Panel;
using Knock.Identity.Api.Modules.Panel.Authorization;
using Knock.Identity.Api.Modules.Safety.Dtos;
using Knock.Identity.Api.Modules.Safety.Services;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Identity.Api.Modules.Safety.Controllers.V1;

/// <summary>Resolución de las apelaciones de personas suspendidas desde el panel (spec 060, R-06). Exige <c>reports:resolve</c> con la sesión del panel (doble factor, spec 033); la persona que resuelve es la del <c>sub</c> del token.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/apelaciones")]
[RequierePermiso(PanelApiPermissions.ReportsResolve)]
internal sealed class AdminApelacionesController(SuspensionAppealResolutionService service) : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>Acepta (reactiva la cuenta) o rechaza la apelación. 404 <c>appeal_not_found</c>; 409 <c>appeal_already_resolved</c>; 403 <c>self_review_not_allowed</c>.</summary>
    [HttpPost("{id}/resolucion")]
    [RequestSizeLimit(AppealResolutionBody.MaxBodyBytes)]
    [ProducesResponseType<AppealResolutionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<ActionResult<AppealResolutionResult>> Resolve([FromRoute] ReportRoute route, [FromBody] AppealResolutionBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        Response.Headers.CacheControl = "private, no-store";
        return await service.ResolveAsync(User.FindFirst("sub")?.Value ?? string.Empty, route.Id!, body.Decision!, cancellationToken);
    }
}
