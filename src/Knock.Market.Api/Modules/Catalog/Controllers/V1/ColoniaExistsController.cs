using Asp.Versioning;
using Knock.Market.Api.Modules.Catalog.Dtos;
using Knock.Market.Api.Modules.Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Modules.Catalog.Controllers.V1;

/// <summary>
/// «¿Existe una colonia con este nombre?» (spec 017, R-06): la consulta con la que Identidad valida la colonia del perfil. Va aparte de
/// <see cref="ColoniasController"/> (que resuelve un punto) para que cada controller tenga una sola responsabilidad.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/colonias")]
[Authorize]
internal sealed class ColoniaExistsController(INeighborhoodNames names) : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>200 en ambos casos; el nombre se compara sin mayúsculas ni acentos y no se registra. El 422 lo da <c>ValidationFilter</c> antes de esta acción.</summary>
    [HttpGet("existe")]
    [ResponseCache(NoStore = true)]
    [ProducesResponseType<NeighborhoodExistsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<ActionResult<NeighborhoodExistsResponse>> Exists(
        [FromQuery] NeighborhoodExistsQuery query, CancellationToken cancellationToken) =>
        new NeighborhoodExistsResponse(await names.ExistsAsync(query.Nombre!, cancellationToken));
}
