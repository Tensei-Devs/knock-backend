using Asp.Versioning;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Requests.Dtos;
using Knock.Market.Api.Modules.Requests.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Modules.Requests.Controllers.V1;

/// <summary>
/// Entrega interna de los eventos de bloqueo de Identidad (spec 043, R-23): solo la llama Identidad con su token de servicio y el permiso <c>bloqueos</c>
/// (<see cref="InternalServiceAuthentication"/>); el gateway no la enruta. Mantiene la proyección <c>bloqueos</c> sin leer la base de Identidad. Nunca se cachea.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/interno/bloqueos")]
[Authorize(Policy = InternalServiceAuthentication.BlocksPolicy)]
[PrivateNoStore]
internal sealed class InternalBlocksController(BlockEventService service) : BusinessRuleController
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, ProblemJson)]
    public async Task<IActionResult> Post(BlockEventBody body, CancellationToken cancellationToken)
    {
        await service.ApplyAsync(body, cancellationToken);
        return Accepted();
    }
}
