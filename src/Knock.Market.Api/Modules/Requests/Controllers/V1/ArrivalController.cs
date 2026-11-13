using Asp.Versioning;
using Knock.Market.Api.Modules.Requests.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Modules.Requests.Controllers.V1;

/// <summary>Confirmación de llegada con el código de cuatro dígitos (spec 022, R-07 a R-11). Sin caché.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/visitas/{id}/llegada")]
[Authorize]
internal sealed class ArrivalController : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>
    /// El técnico elegido confirma que llegó. Código incorrecto: 422 <c>wrong_code</c> con <c>intentosRestantes</c>; al tercero la visita queda bloqueada.
    /// Estados no válidos responden 409 con <c>outside_window</c>, <c>code_used</c>, <c>code_expired</c>, <c>visit_locked</c>, <c>visit_cancelled</c>,
    /// <c>visit_not_active</c> o <c>visit_without_code</c>.
    /// </summary>
    [HttpPost]
    [ResponseCache(NoStore = true)]
    [ProducesResponseType<VisitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public ActionResult<VisitResponse> Arrive(string id, [FromBody] ArrivalBody body) => StatusCode(StatusCodes.Status501NotImplemented);
}
