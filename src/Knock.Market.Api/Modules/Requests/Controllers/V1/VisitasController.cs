using Asp.Versioning;
using Knock.Market.Api.Modules.Requests.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Modules.Requests.Controllers.V1;

/// <summary>Lectura de la visita (spec 022, R-01): el cliente dueño y el técnico elegido ven cada uno su vista. Sin caché.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/visitas")]
[Authorize]
internal sealed class VisitasController : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>Vista del cliente (con el código cuando corresponde) o del técnico (con la dirección y sin código). Cualquier otra persona recibe 404.</summary>
    [HttpGet("{id}")]
    [ResponseCache(NoStore = true)]
    [ProducesResponseType<VisitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public ActionResult<VisitResponse> Get(string id) => StatusCode(StatusCodes.Status501NotImplemented);
}
