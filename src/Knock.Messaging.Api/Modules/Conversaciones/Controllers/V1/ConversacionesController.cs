using Asp.Versioning;
using Knock.Hosting.Mvc;
using Knock.Messaging.Api.Hosting;
using Knock.Messaging.Api.Modules.Conversaciones.Dtos;
using Knock.Messaging.Api.Modules.Conversaciones.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Knock.Messaging.Api.Modules.Conversaciones.Controllers.V1;

/// <summary>
/// Conversaciones de la persona (spec 030, R-04, R-05, R-17 y R-18): crear, leer, listar y el contador de no leídos. Recibe, valida el formato y delega en <see cref="ConversacionService"/>;
/// no toca MongoDB, Redis ni HTTP. La persona es siempre la del <c>sub</c> del token. Nunca se cachea (<c>Cache-Control: private, no-store</c>).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/conversaciones")]
[Authorize]
[PrivateNoStore]
internal sealed class ConversacionesController(ConversacionService service) : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>Crea la conversación del par (solicitud, técnico) o devuelve la que ya existe: 201 la primera vez y 200 después. 409 <c>conversation_closed</c> si está cerrada; 20 por hora por persona.</summary>
    [HttpPost]
    [EnableRateLimiting(MessagingRateLimits.CreateConversationPolicy)]
    [ProducesResponseType<ConversacionView>(StatusCodes.Status201Created)]
    [ProducesResponseType<ConversacionView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, _problemJson)]
    public async Task<IActionResult> Create(CrearConversacionBody body, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(CallerSession.Subject(User), CallerSession.BearerToken(Request), body, cancellationToken);
        return result.Creada
            ? Created(new Uri($"/api/v1/conversaciones/{result.Conversacion.ConversacionId}", UriKind.Relative), result.Conversacion)
            : Ok(result.Conversacion);
    }

    /// <summary>Las conversaciones de la persona, de la más reciente a la más antigua, con paginación por cursor (1 a 20) y el total de no leídos.</summary>
    [HttpGet]
    [ProducesResponseType<ConversacionPage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public Task<ConversacionPage> List([FromQuery] ConversacionesQuery query, CancellationToken cancellationToken) =>
        service.ListAsync(CallerSession.Subject(User), query, cancellationToken);

    /// <summary>La suma de los no leídos de la persona en todas sus conversaciones.</summary>
    [HttpGet("no-leidas")]
    [ProducesResponseType<NoLeidasView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public Task<NoLeidasView> Unread(CancellationToken cancellationToken) =>
        service.UnreadAsync(CallerSession.Subject(User), cancellationToken);

    /// <summary>Una conversación, solo para sus dos participantes; para cualquier otra persona, 404.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType<ConversacionView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public Task<ConversacionView> Get(string id, CancellationToken cancellationToken) =>
        service.GetAsync(CallerSession.Subject(User), id, cancellationToken);
}
