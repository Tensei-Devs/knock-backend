using Asp.Versioning;
using Knock.Messaging.Api.Hosting;
using Knock.Messaging.Api.Modules.Bloqueos.Dtos;
using Knock.Messaging.Api.Modules.Bloqueos.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Messaging.Api.Modules.Bloqueos.Controllers;

/// <summary>Entrega interna de los eventos de bloqueo de Identidad (spec 043, R-23). Fuera del gateway; solo token de servicio con <c>permisos</c> por tipo, igual que los avisos (spec 037, R-11).</summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = InternalNoticeAuthentication.Policy)]
[PrivateNoStore]
[Route("api/v{version:apiVersion}/interno/bloqueos")]
internal sealed class InternoBloqueosController(BloqueosService service) : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>Recibe el evento de un servicio emisor y responde 202, también si ya lo recibió. 401 con token inválido o vencido; 403 <c>service_not_allowed</c> si el emisor no tiene el tipo en <c>permisos</c>; 422 con un tipo o cuerpo fuera del catálogo.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<IActionResult> Post(EventoDeBloqueo body, CancellationToken cancellationToken)
    {
        if (!InternalNoticeAuthentication.PermissionsOf(User).Contains(body.Tipo!))
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                extensions: new Dictionary<string, object?> { ["code"] = InternalNoticeAuthentication.ServiceNotAllowedCode });
        }

        await service.ApplyAsync(body, cancellationToken);
        return Accepted();
    }
}
