using Asp.Versioning;
using Knock.Messaging.Api.Hosting;
using Knock.Messaging.Api.Modules.Notices.Dtos;
using Knock.Messaging.Api.Modules.Notices.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Messaging.Api.Modules.Notices.Controllers.V1;

/// <summary>Preferencias de avisos propias (spec 044, PRE-01). El cambio (PUT) lo define la spec 037.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/me/preferencias-avisos")]
[Authorize]
[PrivateNoStore]
internal sealed class MePreferenciasAvisosController(NoticesService notices) : ControllerBase
{
    /// <summary>Un interruptor por tipo; sin cambios guardados, todos activos salvo promociones; seguridad siempre activa y no apagable.</summary>
    [HttpGet]
    [EnableRateLimiting(RateLimitingExtensions.NoticesReadPolicy)]
    [ProducesResponseType<NoticePreferencesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        User.FindFirst("sub")?.Value is { Length: > 0 } sub ? Ok(await notices.GetPreferencesAsync(sub, cancellationToken)) : Unauthorized();
}
