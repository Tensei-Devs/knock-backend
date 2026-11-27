using Asp.Versioning;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Requests.Services;
using Knock.Market.Api.Modules.Requests.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Modules.Requests.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tecnicos/me/solicitudes")]
[Authorize]
[PrivateNoStore]
internal sealed class BandejaController(BandejaService service) : BusinessRuleController
{
    [HttpGet]
    [ProducesResponseType<InboxResponse>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(401)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(422)]
    [ProducesResponseType<ProblemDetails>(429)]
    public async Task<IActionResult> List([FromQuery] BandejaQuery query, CancellationToken cancellationToken)
    {
        var sub = User.FindFirst("sub")?.Value ?? string.Empty;
        if (!await service.IsTechnicianAsync(sub, cancellationToken))
        {
            return Rule(403, "not_a_technician");
        }
        return Ok(await service.ListAsync(sub, query.Cursor, query.Limit, cancellationToken));
    }
}
