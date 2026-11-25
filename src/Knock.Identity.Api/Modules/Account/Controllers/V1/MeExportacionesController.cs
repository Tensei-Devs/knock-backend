using Asp.Versioning;
using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Modules.Account.Dtos;
using Knock.Identity.Api.Modules.Account.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Identity.Api.Modules.Account.Controllers.V1;

/// <summary>Exportación de los datos propios (spec 039, USR-06).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/me/exportaciones")]
[Authorize]
internal sealed class MeExportacionesController(DataExportService exports, LatestExportService latest) : AccountControllerBase
{
    /// <summary>La ruta no lleva cuerpo: cualquiera que llegue se corta en 1 KiB (413).</summary>
    public const int MaxBodyBytes = 1024;

    /// <summary>USR-06. Pide la descarga de todos los datos propios; se entrega en 48 horas. Una sola en curso por persona.</summary>
    [HttpPost]
    [EnableRateLimiting(IdentityRateLimits.ExportPolicy)]
    [RequestSizeLimit(MaxBodyBytes)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RequestExport(CancellationToken cancellationToken)
    {
        if (!TryGetCaller(out var sub, out _))
        {
            return UnauthorizedProblem();
        }

        await exports.RequestAsync(sub, cancellationToken);
        return Accepted();
    }

    /// <summary>
    /// USR-06b. La app lo llama al abrir el aviso <c>exportacion_lista</c>: <c>en_preparacion</c>, <c>vencida</c> o <c>lista</c> con un enlace firmado de 24 horas que se genera en este momento. 404 <c>export_not_found</c> si nunca
    /// pidió una; 503 <c>export_unavailable</c> si el contenedor no responde. Solo la persona del token; sin caché y el enlace no se registra.
    /// </summary>
    [HttpGet("ultima")]
    [EnableRateLimiting(IdentityRateLimits.ExportLatestPolicy)]
    [ProducesResponseType<LatestExportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetLatest(CancellationToken cancellationToken)
    {
        if (!TryGetCaller(out var sub, out _))
        {
            return UnauthorizedProblem();
        }

        Response.Headers.CacheControl = "private, no-store";
        return Ok(await latest.GetAsync(sub, cancellationToken));
    }
}
