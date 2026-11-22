using Asp.Versioning;
using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using Knock.Market.Api.Modules.Tecnicos.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Market.Api.Modules.Tecnicos.Controllers.V1;

/// <summary>Explorar (spec 046, TEC-14): lectura pública con caché en el servidor. Recibe, valida y delega.</summary>
[ApiController]
[ApiVersion("1.0")]
[AllowAnonymous]
internal sealed class ExplorarController(TechnicianExploreService explore) : ControllerBase
{
    private const string _problemJson = TechnicianProblems.ProblemJson;

    /// <summary>Bloques de Explorar (máximo 10 elementos cada uno). Sin sesión, solo los públicos; con sesión suma trabajos reales y cerca de ti.</summary>
    [HttpGet("api/v{version:apiVersion}/explorar")]
    [EnableRateLimiting(MarketRateLimits.ExplorePolicy)]
    [ProducesResponseType<ExploreResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<IActionResult> Explore([FromQuery] ExploreQuery query, CancellationToken cancellationToken)
    {
        var signedIn = !string.IsNullOrWhiteSpace(User.FindFirst("sub")?.Value);
        Response.Headers.CacheControl = signedIn ? "private, no-store" : "public, max-age=60";
        var outcome = await explore.ExploreAsync(query, signedIn, cancellationToken);
        return outcome.Status == ExploreStatus.Ok ? Ok(outcome.Response) : TechnicianProblems.Rule(this, StatusCodes.Status422UnprocessableEntity, ErrorCodes.ColoniaNotFoundCode, "colonia");
    }
}
