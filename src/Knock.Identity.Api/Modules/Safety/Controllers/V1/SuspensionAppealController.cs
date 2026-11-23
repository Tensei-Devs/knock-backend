using System.Globalization;
using Asp.Versioning;
using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Modules.Safety.Dtos;
using Knock.Identity.Api.Modules.Safety.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Identity.Api.Modules.Safety.Controllers.V1;

/// <summary>
/// Apelación pública de una persona suspendida (spec 060). No usa el JWT de Knock: lleva el <c>idToken</c> de Firebase de un inicio de sesión por SMS reciente en <c>Authorization: Bearer</c> y no abre sesión.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/suspension/apelacion")]
[AllowAnonymous]
[EnableRateLimiting(IdentityRateLimits.SuspensionAppealPolicy)]
internal sealed class SuspensionAppealController(SuspensionAppealService service) : ControllerBase
{
    private const string _problemJson = "application/problem+json";
    private const string _bearer = "Bearer ";
    private const int _maxTokenLength = 4096;

    /// <summary>Presenta la apelación de la suspensión vigente (R-01 a R-05). 200 con el estado; 401, 404 uniforme, 409 <c>appeal_not_allowed</c>.</summary>
    [HttpPost]
    [RequestSizeLimit(SuspensionAppealBody.MaxBodyBytes)]
    [ProducesResponseType<SuspensionAppealView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<IActionResult> Appeal([FromBody] SuspensionAppealBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        Response.Headers.CacheControl = "no-store";
        return await ToResultAsync(await service.AppealAsync(IdToken(), body.Texto ?? string.Empty, cancellationToken));
    }

    /// <summary>Estado de la apelación (R-09). 200 con estado y fechas; 401; 404 uniforme; 429.</summary>
    [HttpGet]
    [ProducesResponseType<SuspensionAppealView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return await ToResultAsync(await service.GetAsync(IdToken(), cancellationToken));
    }

    // El idToken viaja en Authorization; ausente, vacío o demasiado largo se trata como un token que no sirve (401), sin distinguir el motivo.
    private string IdToken()
    {
        var header = Request.Headers.Authorization.ToString();
        return header.StartsWith(_bearer, StringComparison.OrdinalIgnoreCase) && header.Length - _bearer.Length is > 0 and <= _maxTokenLength
            ? header[_bearer.Length..].Trim()
            : string.Empty;
    }

    private async Task<IActionResult> ToResultAsync(SuspensionAppealOutcome outcome)
    {
        switch (outcome)
        {
            case SuspensionAppealOutcome.Found found:
                return Ok(found.Appeal);
            case SuspensionAppealOutcome.RateLimited limited:
                Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(limited.RetryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                await ProblemResponses.WriteAsync(HttpContext, StatusCodes.Status429TooManyRequests, "Too Many Requests");
                return new EmptyResult();
            default:
                return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: "Invalid credentials.");
        }
    }
}
