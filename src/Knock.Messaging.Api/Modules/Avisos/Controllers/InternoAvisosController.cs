using Asp.Versioning;
using Knock.Hosting.Mvc;
using Knock.Messaging.Api.Hosting;
using Knock.Messaging.Api.Modules.Avisos.Dtos;
using Knock.Messaging.Api.Modules.Avisos.Push;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Messaging.Api.Modules.Avisos.Controllers;

/// <summary>Entrega interna de eventos de aviso de Mercado e Identidad (spec 037, R-11). Fuera del gateway; solo token de servicio con <c>permisos</c> por tipo.</summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = InternalNoticeAuthentication.Policy)]
[PrivateNoStore]
[Route("api/v{version:apiVersion}/interno/avisos")]
internal sealed class InternoAvisosController(DespachadorDeAvisos dispatcher, IDeviceTokens devices) : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>Recibe el evento de un servicio emisor y responde 202, también si ya lo recibió. 401 con token inválido o vencido; 403 <c>service_not_allowed</c> si el emisor no tiene el tipo en <c>permisos</c>; 422 con un tipo o cuerpo fuera del catálogo.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<IActionResult> Post(AvisoInterno body, CancellationToken cancellationToken)
    {
        if (!InternalNoticeAuthentication.PermissionsOf(User).Contains(body.Tipo!))
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                extensions: new Dictionary<string, object?> { ["code"] = InternalNoticeAuthentication.ServiceNotAllowedCode });
        }

        if (body.Tipo == Domain.AvisoRules.SessionRevokedEvent)
        {
            await devices.DeleteBySessionAsync(body.Sid!, cancellationToken);
        }
        else
        {
            await dispatcher.DispatchAsync(new AvisoEvent(body.EventoId!, body.Tipo!, body.DestinatarioId!, body.Recurso, null, body.Rol), cancellationToken);
        }

        return Accepted();
    }
}
