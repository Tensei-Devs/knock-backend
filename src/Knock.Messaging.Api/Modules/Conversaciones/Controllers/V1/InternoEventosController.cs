using Asp.Versioning;
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
    [HttpPost]
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
