using Asp.Versioning;
using Knock.Messaging.Api.Hosting;
using Knock.Messaging.Api.Modules.Avisos.Domain;
using Knock.Messaging.Api.Modules.Avisos.Dtos;
using Knock.Messaging.Api.Modules.Avisos.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Messaging.Api.Modules.Avisos.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[PrivateNoStore]
[EnableRateLimiting(MessagingRateLimits.DevicesPolicy)]
[Route("api/v{version:apiVersion}/dispositivos")]
internal sealed class DispositivosController(DispositivosService service) : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>Registra el token de notificaciones del dispositivo (spec 037, R-01): 201 la primera vez, 200 si ya estaba. 409 si otra persona lo registró a la vez; 503 con el envío de avisos apagado; 20 por hora por persona.</summary>
    [HttpPost]
    [ProducesResponseType<IdDispositivo>(StatusCodes.Status201Created)]
    [ProducesResponseType<IdDispositivo>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, _problemJson)]
    public async Task<IActionResult> Post(RegistroDispositivo body, CancellationToken cancellationToken)
    {
        if (!service.Available)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var result = await service.RegisterAsync(User.FindFirst("sub")!.Value, User.FindFirst("sid")!.Value, body.Plataforma!, body.Identificador!, cancellationToken);
        return result.Status switch
        {
            DeviceRegistrationStatus.Created => Created(new Uri($"{Request.Path}/{result.Id}", UriKind.Relative), new IdDispositivo(result.Id!)),
            DeviceRegistrationStatus.Existing => Ok(new IdDispositivo(result.Id!)),
            _ => Conflict(),
        };
    }

    /// <summary>Borra el dispositivo de la persona (spec 037, R-02): 204 también si ya no existe o es de otra persona.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, _problemJson)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        if (!service.Available)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        // R-02: 204 también si el dispositivo ya no existe o es de otra persona (no se revela cuál de los dos).
        await service.DeleteAsync(User.FindFirst("sub")!.Value, id, cancellationToken);
        return NoContent();
    }
}
