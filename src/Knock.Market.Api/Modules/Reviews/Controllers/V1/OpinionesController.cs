using Asp.Versioning;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Reviews.Dtos;
using Knock.Market.Api.Modules.Reviews.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Market.Api.Modules.Reviews.Controllers.V1;

/// <summary>
/// Opiniones y reputación del técnico (spec 051, OPI-01 a OPI-03). Recibe, valida el formato y delega en <see cref="ReviewService"/>; no toca MongoDB. La persona es la del <c>sub</c> del token
/// (R-18). Los errores de negocio salen como ProblemDetails con <c>code</c>. Nunca se cachea en el cliente (<c>Cache-Control: private, no-store</c>).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
[PrivateNoStore]
[EnableRateLimiting(MarketRateLimits.ReviewsPolicy)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
internal sealed class OpinionesController(ReviewService service) : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>OPI-01: opiniones publicadas de un técnico visible con su distribución por estrellas, de la más nueva a la más vieja, por cursor.</summary>
    [HttpGet("tecnicos/{id}/opiniones")]
    [ProducesResponseType<ReviewPage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    public async Task<IActionResult> List([FromRoute] TechnicianReviewsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Ok(await service.ListAsync(query.Id!, query.Cursor, query.Limite, cancellationToken));
    }

    /// <summary>OPI-02: el cliente de una visita terminada la califica una sola vez, hasta 30 días después.</summary>
    [HttpPost("visitas/{id}/opinion")]
    [RequestSizeLimit(CreateReviewBody.MaxBodyBytes)]
    [ProducesResponseType<CreatedReview>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    public async Task<IActionResult> Create([FromRoute] ReviewVisitRoute route, [FromBody] CreateReviewBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(route);
        var created = await service.CreateAsync(Subject(), route.Id!, body, cancellationToken);
        return Created(new Uri($"/api/v1/opiniones/{created.Id}", UriKind.Relative), created);
    }

    /// <summary>OPI-03: el técnico calificado responde una sola vez a una opinión suya.</summary>
    [HttpPost("opiniones/{id}/respuesta")]
    [RequestSizeLimit(AnswerReviewBody.MaxBodyBytes)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    public async Task<IActionResult> Answer([FromRoute] ReviewRoute route, [FromBody] AnswerReviewBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(route);
        await service.AnswerAsync(Subject(), route.Id!, body, cancellationToken);
        return Ok();
    }

    private string Subject() => User.FindFirst("sub")?.Value ?? string.Empty;
}
