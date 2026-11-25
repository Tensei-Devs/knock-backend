using Asp.Versioning;
using Knock.Hosting.Mvc;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Requests.Dtos;
using Knock.Market.Api.Modules.Requests.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Market.Api.Modules.Requests.Controllers.V1;

/// <summary>Referencia de mercado de precios (spec 048, COT-03): solo para cuentas con perfil de técnico; un oficio inexistente o un tipo fuera de la lista responden 422 sin repetir el valor (validador).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/cotizaciones/referencia-mercado")]
[Authorize]
[PrivateNoStore]
internal sealed class MarketReferenceController(MarketReferenceService service) : BusinessRuleController
{
    [HttpGet]
    [EnableRateLimiting(MarketRateLimits.MarketReferencePolicy)]
    [ProducesResponseType<MarketReference>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, ProblemJson)]
    public async Task<IActionResult> Get([FromQuery] MarketReferenceQuery query, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(Subject(), query, cancellationToken);
        return result.Status == MarketReferenceStatus.Ok ? Ok(result.Reference) : Forbid();
    }
}
