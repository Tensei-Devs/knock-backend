using Asp.Versioning;
using Knock.Market.Api.Modules.Requests.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Modules.Requests.Controllers.V1;

/// <summary>
/// Solicitudes de cotización (spec 020, HU-024): crear, listar las propias y ver el detalle según el rol. Reciben, validan el formato y delegan;
/// no tocan MongoDB. Ninguna respuesta se cachea (<c>Cache-Control: private, no-store</c>).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/solicitudes")]
[Authorize]
internal sealed class SolicitudesController : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>Crea la solicitud en estado <c>enviada</c> y con <c>venceEn</c> a 48 h.</summary>
    [HttpPost]
    [ResponseCache(NoStore = true)]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, _problemJson)]
    public ActionResult<RequestResponse> Create([FromBody] CreateRequestBody body) => NotImplemented();

    /// <summary>Solicitudes del <c>sub</c> del token, de la más reciente a la más antigua, con paginación por cursor.</summary>
    [HttpGet]
    [ResponseCache(NoStore = true)]
    [ProducesResponseType<RequestPage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public ActionResult<RequestPage> List([FromQuery] ListRequestsQuery query) => NotImplemented();

    /// <summary>Detalle: vista del cliente dueño, o vista acotada del técnico elegible o que ya cotizó. Cualquier otra persona recibe 404.</summary>
    [HttpGet("{id}")]
    [ResponseCache(NoStore = true)]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public ActionResult<RequestResponse> Get(string id) => NotImplemented();

    // Contrato primero (KNOCK-59, borrador): el servicio llega en los siguientes commits de la rama.
    private StatusCodeResult NotImplemented() => StatusCode(StatusCodes.Status501NotImplemented);
}
