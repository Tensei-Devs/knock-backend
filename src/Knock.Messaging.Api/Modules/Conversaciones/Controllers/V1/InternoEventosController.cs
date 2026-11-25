using Asp.Versioning;
using Knock.Hosting.Mvc;
using Knock.Messaging.Api.Hosting;
using Knock.Messaging.Api.Modules.Conversaciones.Dtos;
using Knock.Messaging.Api.Modules.Conversaciones.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Messaging.Api.Modules.Conversaciones.Controllers.V1;

/// <summary>
/// Entrega interna de eventos de negocio de Mercado a Mensajería (spec 054, R-03). Fuera del gateway; usa el token de servicio de <c>interno/avisos</c> con el permiso <c>CotizacionElegida</c>.
/// <c>CotizacionElegida</c> cierra las demás conversaciones de la solicitud sin borrar mensajes (spec 048, CA-21) y repetirlo no cambia nada.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = InternalNoticeAuthentication.Policy)]
[PrivateNoStore]
[Route("api/v{version:apiVersion}/interno/eventos")]
internal sealed class InternoEventosController(CierrePorEleccionService closing) : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>Recibe <c>CotizacionElegida</c> y responde 202, también si ya lo recibió. 401 con token inválido; 403 <c>service_not_allowed</c>; 422 con un cuerpo fuera del catálogo.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<IActionResult> Post(EventoInterno body, CancellationToken cancellationToken)
    {
        if (!InternalNoticeAuthentication.PermissionsOf(User).Contains(body.Tipo!))
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                extensions: new Dictionary<string, object?> { ["code"] = InternalNoticeAuthentication.ServiceNotAllowedCode });
        }

        await closing.CloseOthersAsync(body.SolicitudId!, body.TecnicoUsuarioId!, cancellationToken);
        return Accepted();
    }
}
