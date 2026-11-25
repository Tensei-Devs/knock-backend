using Asp.Versioning;
using Knock.Hosting.Mvc;
using Knock.Messaging.Api.Hosting;
using Knock.Messaging.Api.Modules.Notices.Dtos;
using Knock.Messaging.Api.Modules.Notices.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Messaging.Api.Modules.Notices.Controllers.V1;

/// <summary>Preferencias de avisos propias (spec 044, PRE-01 y PRE-02). La persona es siempre la del <c>sub</c> del token (R-15).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/me/preferencias-avisos")]
[Authorize]
[PrivateNoStore]
internal sealed class MePreferenciasAvisosController(NoticesService notices) : ControllerBase
{
    /// <summary>Tope del cuerpo del PUT: un renglón por tipo cabe de sobra en 2 KiB; más responde 413 (spec 009).</summary>
    public const int MaxBodyBytes = 2048;

    /// <summary>Un interruptor por tipo; sin cambios guardados, todos activos salvo promociones; seguridad siempre activa y no apagable.</summary>
    [HttpGet]
    [EnableRateLimiting(MessagingRateLimits.NoticesReadPolicy)]
    [ProducesResponseType<NoticePreferencesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        User.FindFirst("sub")?.Value is { Length: > 0 } sub ? Ok(await notices.GetPreferencesAsync(sub, cancellationToken)) : Unauthorized();

    /// <summary>Cambia los interruptores indicados y devuelve las preferencias vigentes. Los avisos de seguridad no se pueden apagar (422).</summary>
    [HttpPut]
    [EnableRateLimiting(MessagingRateLimits.NoticePreferencesPolicy)]
    [BodyLimit(MaxBodyBytes)]
    [ProducesResponseType<NoticePreferencesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Update(UpdateNoticePreferencesRequest body, CancellationToken cancellationToken) =>
        User.FindFirst("sub")?.Value is { Length: > 0 } sub ? Ok(await notices.UpdatePreferencesAsync(sub, body, cancellationToken)) : Unauthorized();
}
