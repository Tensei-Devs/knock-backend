using Asp.Versioning;
using Knock.Market.Api.Modules.Requests.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Modules.Requests.Controllers.V1;

/// <summary>Aviso de confianza de la visita (spec 022, R-12 y R-13): prepara un texto; no guarda contactos ni envía mensajes. Sin caché.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/visitas/{id}/aviso")]
[Authorize]
internal sealed class TrustNoticeController : ControllerBase
{
    private const string _problemJson = "application/problem+json";

    /// <summary>El cliente dueño obtiene el texto para compartir con alguien de confianza. Solo con la visita <c>confirmada</c> o <c>en_curso</c>.</summary>
    [HttpPost]
    [ResponseCache(NoStore = true)]
    [ProducesResponseType<TrustNotice>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    public ActionResult<TrustNotice> Prepare(string id) => StatusCode(StatusCodes.Status501NotImplemented);
}
