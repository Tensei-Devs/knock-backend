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
    [HttpPost]
    public async Task<IActionResult> Post(RegistroDispositivo body, CancellationToken cancellationToken)
    {
        if (!service.Available)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var result = await service.RegisterAsync(User.FindFirst("sub")!.Value, User.FindFirst("sid")!.Value, body.Plataforma!, body.Identificador!, cancellationToken);
        return result.Status switch
        {
            DeviceRegistrationStatus.Created => Created(new Uri($"{Request.Path}/{result.Id}", UriKind.Relative), new { id = result.Id }),
            DeviceRegistrationStatus.Existing => Ok(new { id = result.Id }),
            _ => Conflict(),
        };
    }

    [HttpDelete("{id}")]
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
