using Asp.Versioning;
using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using Knock.Market.Api.Modules.Tecnicos.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Market.Api.Modules.Tecnicos.Controllers.V1;

/// <summary>Mapa de técnicos (spec 046, TEC-04) (TEC-04): lectura pública con caché en el servidor. Recibe, valida y delega.</summary>
[ApiController]
[ApiVersion("1.0")]
[AllowAnonymous]
internal sealed class MapaController(TechnicianMapService map) : ControllerBase
{
    private const string _problemJson = TechnicianProblems.ProblemJson;

    /// <summary>Técnicos de la zona visible, agrupados por celda según el zoom (hasta 200 grupos). Ninguna coordenada de la petición se guarda ni se registra.</summary>
    [HttpGet("api/v{version:apiVersion}/mapa/tecnicos")]
    [EnableRateLimiting(MarketRateLimits.MapPolicy)]
    [ProducesResponseType<MapResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<IActionResult> Map([FromQuery] MapQuery query, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "public, max-age=10";
        var outcome = await map.SearchAsync(query, cancellationToken);
        return outcome.Status == MapStatus.Ok ? Ok(outcome.Response) : TechnicianProblems.Rule(this, StatusCodes.Status422UnprocessableEntity, ErrorCodes.TradeInvalidCode, "oficio");
    }
}
