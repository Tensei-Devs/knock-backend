using Asp.Versioning;
using Knock.Hosting.Mvc;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using Knock.Market.Api.Modules.Tecnicos.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Modules.Tecnicos.Controllers.V1;

/// <summary>
/// Ruta interna que recibe <c>TecnicoOcultado</c> (spec 053, R-20): solo la llama Identidad con su token de servicio (<see cref="InternalServiceAuthentication"/>, permiso
/// <c>tecnico-ocultado</c>) y el gateway no la enruta. Responde 202 sin cuerpo, también si el técnico ya estaba oculto o no existe, para que el procesador de la bandeja no reintente sin fin.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/interno/tecnicos-ocultados")]
[Authorize(Policy = InternalServiceAuthentication.TechnicianHiddenPolicy)]
[PrivateNoStore]
internal sealed class InternalTechnicianHiddenController(TechnicianHideService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, TechnicianProblems.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, TechnicianProblems.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, TechnicianProblems.ProblemJson)]
    public async Task<IActionResult> Hide([FromBody] TechnicianHiddenEventBody body, CancellationToken cancellationToken)
    {
        await service.HideAsync(body.UsuarioId!, body.ReporteId!, cancellationToken);
        return Accepted();
    }
}
