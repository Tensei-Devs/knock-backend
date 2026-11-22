using Asp.Versioning;
using Knock.Messaging.Api.Hosting;
using Knock.Messaging.Api.Modules.Notices.Dtos;
using Knock.Messaging.Api.Modules.Notices.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Messaging.Api.Modules.Notices.Controllers.V1;

/// <summary>
/// Avisos dentro de la app (spec 044, AVI-01 a AVI-04). Ninguna ruta, consulta ni cuerpo acepta el identificador de otra persona: la persona es siempre la del <c>sub</c> del token
/// (R-15) y un aviso ajeno no se distingue de uno que no existe (R-12). No crea avisos (R-14).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/avisos")]
[Authorize]
[PrivateNoStore]
internal sealed class AvisosController(NoticesService notices) : ControllerBase
{
    /// <summary>Límite del cuerpo de los <c>POST</c>: no reciben cuerpo, 1 KiB basta; más responde 413 (spec 009).</summary>
    public const int MaxBodyBytes = 1024;

    public const string NoticeNotFoundCode = "notice_not_found";

    /// <summary>Lista los avisos de quien llama, del más reciente al más antiguo, por cursor (20 por omisión, 50 como máximo).</summary>
    [HttpGet]
    [EnableRateLimiting(RateLimitingExtensions.NoticesReadPolicy)]
    [ProducesResponseType<NoticeListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> List([FromQuery] ListNoticesQuery query, CancellationToken cancellationToken) =>
        Sub() is { } sub ? Ok(await notices.ListAsync(sub, query, cancellationToken)) : Unauthorized();

    /// <summary>Cantidad de avisos sin leer, hasta 99; no se cachea.</summary>
    [HttpGet("no-leidos/contador")]
    [EnableRateLimiting(RateLimitingExtensions.NoticesReadPolicy)]
    [ProducesResponseType<UnreadCountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CountUnread(CancellationToken cancellationToken) =>
        Sub() is { } sub ? Ok(await notices.CountUnreadAsync(sub, cancellationToken)) : Unauthorized();

    /// <summary>Marca un aviso como leído; repetirlo responde 204 igual. Uno ajeno o inexistente responde 404 <c>notice_not_found</c>.</summary>
    [HttpPost("{id}/leido")]
    [EnableRateLimiting(RateLimitingExtensions.NoticesWritePolicy)]
    [RequestSizeLimit(MaxBodyBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> MarkRead([FromRoute] MarkNoticeReadRequest request, CancellationToken cancellationToken)
    {
        if (Sub() is not { } sub)
        {
            return Unauthorized();
        }

        if (await notices.MarkReadAsync(sub, request.Id!, cancellationToken))
        {
            return NoContent();
        }

        await ProblemResponses.WriteAsync(HttpContext, StatusCodes.Status404NotFound, "Not Found", NoticeNotFoundCode);
        return new EmptyResult();
    }

    /// <summary>Marca como leídos todos los avisos de quien llama y solo los suyos.</summary>
    [HttpPost("leidos")]
    [EnableRateLimiting(RateLimitingExtensions.NoticesWritePolicy)]
    [RequestSizeLimit(MaxBodyBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        if (Sub() is not { } sub)
        {
            return Unauthorized();
        }

        await notices.MarkAllReadAsync(sub, cancellationToken);
        return NoContent();
    }

    private string? Sub() => User.FindFirst("sub")?.Value is { Length: > 0 } sub ? sub : null;
}
