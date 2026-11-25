using Asp.Versioning;
using Knock.Hosting.Mvc;
using Knock.Market.Api.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Modules.Requests.Controllers;

/// <summary>
/// Base de los controllers de Requests con sesión: versión 1.0, token obligatorio y respuestas que nunca se cachean (<c>Cache-Control: private, no-store</c>). Cada controller declara su ruta.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[PrivateNoStore]
internal abstract class PrivateRequestsController : BusinessRuleController;

/// <summary>
/// Base de los controllers de Requests con sesión que documentan en conjunto los errores comunes del contrato: 400, 401, 403, 404, 409, 413, 422 y 429, todos como <c>application/problem+json</c>.
/// </summary>
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, ProblemJson)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, ProblemJson)]
internal abstract class DocumentedRequestsController : PrivateRequestsController;
