using Asp.Versioning;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Catalog.Dtos;
using Knock.Market.Api.Modules.Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Market.Api.Modules.Catalog.Controllers.V1;

/// <summary>
/// Oficios pedidos en «Otro oficio» (spec 045, OFP-01). Con sesión de la app; la persona es la del <c>sub</c> del token y ningún cuerpo acepta identificadores. Comparte el límite de
/// <see cref="RateLimitingExtensions.RegisterTechnicianPolicy"/> (5 por hora por persona) porque es parte del flujo de registro como técnico.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/oficios-pedidos")]
[Authorize]
[PrivateNoStore]
[EnableRateLimiting(RateLimitingExtensions.RegisterTechnicianPolicy)]
internal sealed class TradeRequestsController(TradeRequestService service) : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>
    /// Guarda el oficio escrito en «Otro oficio» (OFP-01): 201 con el registro. Repetir el mismo texto (ignorando acentos, mayúsculas y espacios) devuelve el registro existente. Quien no tiene perfil de técnico
    /// recibe 404 sin más datos; un cuerpo ilegible, 400; un texto inválido, 422 <c>errors.texto</c>.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(TradeRequestBodies.MaxBodyBytes)]
    [ProducesResponseType<TradeRequestResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public async Task<IActionResult> Create([FromBody] TradeRequestBody body, CancellationToken cancellationToken)
    {
        if (User.FindFirst("sub")?.Value is not { Length: > 0 } subject)
        {
            return Unauthorized();
        }

        var saved = await service.CreateAsync(subject, body, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, saved);
    }
}
