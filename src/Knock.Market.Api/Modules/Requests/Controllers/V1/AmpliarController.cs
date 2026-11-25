using Asp.Versioning;
using Knock.Hosting.Mvc;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;
using Knock.Market.Api.Modules.Requests.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Market.Api.Modules.Requests.Controllers.V1;

/// <summary>
/// «Pedir también a otros» (spec 048, SOL-05): adelanta la oleada siguiente de la spec 028. La dueña es siempre la del <c>sub</c> del token; una solicitud ajena responde 404.
/// No recibe cuerpo y no usa <c>Idempotency-Key</c>: la regla de estado impide pedir dos veces la misma oleada.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/solicitudes/{id}/ampliar")]
[Authorize]
[PrivateNoStore]
internal sealed class AmpliarController(RepartoService service) : BusinessRuleController
{
    [HttpPost]
    [RequestSizeLimit(1024)]
    [EnableRateLimiting(MarketRateLimits.ExtendRequestPolicy)]
    [ProducesResponseType<ExtendRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, ProblemJson)]
    public async Task<IActionResult> Extend(ExtendRequestRoute route, CancellationToken cancellationToken)
    {
        var status = await service.ExtendAsync(route.Id!, Subject(), cancellationToken);
        return status switch
        {
            ExtendStatus.Extended => Ok(new ExtendRequestResponse(route.Id!, "ampliada")),
            ExtendStatus.NotExtendable => Rule(StatusCodes.Status409Conflict, RequestErrorCodes.RequestNotExtendableCode),
            _ => NotFound(),
        };
    }
}
