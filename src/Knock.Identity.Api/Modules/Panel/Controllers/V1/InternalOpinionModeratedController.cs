using Asp.Versioning;
using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Modules.Panel.Services;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Identity.Api.Modules.Panel.Controllers.V1;

/// <summary>
/// Entrega interna de <c>OpinionModerada</c> de Mercado (spec 061, R-06): solo la llama Mercado con su token de servicio y el permiso <c>opinion-moderada</c> (<see cref="InternalServiceTokenAttribute"/>);
/// el gateway no la enruta. Deja la auditoría del panel y responde 202, también si el evento ya estaba auditado. 503 <c>audit_unavailable</c> si no se pudo escribir, para que quien entrega reintente.
/// Nunca se cachea.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/interno/opiniones-moderadas")]
[InternalServiceToken(Permission)]
internal sealed class InternalOpinionModeratedController(OpinionModeradaConsumer consumer) : ControllerBase
{
    public const string Permission = "opinion-moderada";
    private const string _problemJson = "application/problem+json";

    [HttpPost]
    [RequestSizeLimit(2048)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, _problemJson)]
    public async Task<IActionResult> Post([FromBody] OpinionModeratedEventBody body, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        await consumer.ConsumeAsync(body, cancellationToken);
        return Accepted();
    }
}
