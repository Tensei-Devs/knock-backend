using Microsoft.AspNetCore.Mvc;

namespace Knock.Identity.Api.Hosting;

/// <summary>
/// Base de los endpoints internos que reciben un evento entregado por Mercado (<c>POST</c>, 202): límite de cuerpo, respuestas de error documentadas y <c>Cache-Control: private, no-store</c>.
/// Cada controlador concreto fija su ruta, su permiso (<see cref="InternalServiceTokenAttribute"/>) y qué hace con el evento.
/// </summary>
internal abstract class InternalEventDeliveryController<TBody> : ControllerBase
{
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
    public async Task<IActionResult> Post([FromBody] TBody body, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        await ConsumeAsync(body, cancellationToken);
        return Accepted();
    }

    protected abstract Task ConsumeAsync(TBody body, CancellationToken cancellationToken);
}
