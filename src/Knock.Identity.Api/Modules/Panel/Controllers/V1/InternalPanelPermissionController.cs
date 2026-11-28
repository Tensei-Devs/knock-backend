using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using FluentValidation;
using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Modules.Panel.Authorization;
using Knock.Identity.Api.Modules.Panel.Domain;
using Knock.Identity.Api.Modules.Panel.Services;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Identity.Api.Modules.Panel.Controllers.V1;

/// <summary>Cuerpo de <c>POST /api/v1/interno/permisos-del-panel</c> (spec 061): los claims <c>sub</c>, <c>sid</c> y <c>pv</c> del token del panel y el permiso que la ruta de otro servicio exige. Sin campos de más.</summary>
internal sealed record PanelPermissionCheckBody(string? Sub, string? Sid, int? Pv, string? Permiso)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}

/// <summary>Respuesta de la consulta de permiso: solo si se permite o no, sin motivo (no revela roles ni estado de la cuenta).</summary>
internal sealed record PanelPermissionCheckResponse(bool Permitido);

/// <summary>Formato de la consulta: identificadores de caracteres seguros, versión no negativa y un permiso de la lista cerrada; si no, 422 sin repetir el valor.</summary>
internal sealed class PanelPermissionCheckBodyValidator : AbstractValidator<PanelPermissionCheckBody>
{
    public const string FormatReason = "The value is not valid.";

    public PanelPermissionCheckBodyValidator()
    {
        RuleFor(x => x.Sub).Must(value => AuditoriaPanel.IsSafeResourceId(value)).WithMessage(FormatReason);
        RuleFor(x => x.Sid).Must(value => AuditoriaPanel.IsSafeResourceId(value)).WithMessage(FormatReason);
        RuleFor(x => x.Pv).NotNull().GreaterThanOrEqualTo(0).WithMessage(FormatReason);
        RuleFor(x => x.Permiso).Must(PanelPermisos.IsKnown).WithMessage(FormatReason);
        RuleFor(x => x.Extra).Must(extra => extra is null || extra.Count == 0).WithMessage(FormatReason);
    }
}

/// <summary>
/// Consulta interna de permisos del panel para otros servicios (spec 061, apertura de la puerta de Mercado heredada de la 045): solo la llama Mercado con su token de servicio y el permiso
/// <c>panel-permiso</c> (<see cref="InternalServiceTokenAttribute"/>); el gateway no la enruta. Decide con los roles vigentes en el servidor (spec 033, R-21), nunca con lo que diga quien consulta;
/// el segundo factor ya se cumplió al emitir el token. Nunca se cachea.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/interno/permisos-del-panel")]
[InternalServiceToken(Permission)]
internal sealed class InternalPanelPermissionController(PermisoPanelHandler decider) : ControllerBase
{
    public const string Permission = "panel-permiso";
    private const string _problemJson = "application/problem+json";

    [HttpPost]
    [RequestSizeLimit(1024)]
    [ProducesResponseType<PanelPermissionCheckResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, _problemJson)]
    public async Task<ActionResult<PanelPermissionCheckResponse>> Check([FromBody] PanelPermissionCheckBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        Response.Headers.CacheControl = "private, no-store";
        var allowed = await decider.IsAllowedAsync(body.Sub!, body.Sid!, body.Pv!.Value, body.Permiso!, cancellationToken);
        return Ok(new PanelPermissionCheckResponse(allowed));
    }
}
