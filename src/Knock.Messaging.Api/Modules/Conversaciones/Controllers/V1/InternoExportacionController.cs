using Asp.Versioning;
using Knock.Messaging.Api.Hosting;
using Knock.Messaging.Api.Modules.Conversaciones.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Messaging.Api.Modules.Conversaciones.Controllers.V1;

/// <summary>
/// Ruta interna de la exportación de datos personales (spec 039, R-22; spec 054, T-09): solo la llama Identidad con su token de servicio (permiso <c>exportacion</c>, emisor <c>knock-identity</c>);
/// el gateway no la enruta. El token lleva el <c>usuarioId</c> (el <c>sub</c> de la persona) y debe coincidir con la ruta: un token capturado no sirve para otra persona. Devuelve únicamente los
/// mensajes que la persona escribió. Nunca se cachea ni se registra su contenido.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = InternalNoticeAuthentication.Policy)]
[PrivateNoStore]
[Route("api/v{version:apiVersion}/interno/cuentas/{usuarioId}")]
internal sealed class InternoExportacionController(ExportacionService service) : ControllerBase
{
    public const string ExportPermission = "exportacion";
    public const string PersonClaim = "usuarioId";
    public const int MaxPersonLength = 64;

    /// <summary>Mensajes propios de la persona agrupados por conversación. 401 con token inválido; 403 <c>service_not_allowed</c> si falta el permiso, el emisor no es Identidad o el token es de otra persona.</summary>
    [HttpGet("exportacion")]
    [ProducesResponseType<ExportacionMensajeriaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> Export(string usuarioId, CancellationToken cancellationToken)
    {
        if (!IsAllowed(usuarioId))
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                extensions: new Dictionary<string, object?> { ["code"] = InternalNoticeAuthentication.ServiceNotAllowedCode });
        }

        return Ok(await service.ExportAsync(usuarioId, cancellationToken));
    }

    private bool IsAllowed(string usuarioId) =>
        usuarioId.Length is > 0 and <= MaxPersonLength
        && InternalNoticeAuthentication.PermissionsOf(User).Contains(ExportPermission)
        && string.Equals(User.FindFirst("iss")?.Value, InternalNoticeAuthentication.IdentityIssuer, StringComparison.Ordinal)
        && string.Equals(User.FindFirst(PersonClaim)?.Value, usuarioId, StringComparison.Ordinal);
}
