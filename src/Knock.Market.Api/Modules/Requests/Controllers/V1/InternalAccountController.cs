using Asp.Versioning;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Requests.Dtos;
using Knock.Market.Api.Modules.Requests.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Modules.Requests.Controllers.V1;

/// <summary>
/// Rutas internas de la cuenta (spec 039, R-22, R-24 y R-25): solo las llama Identidad con su token de servicio (<see cref="InternalServiceAuthentication"/>, permiso <c>cuenta</c>) y el
/// gateway no las enruta. El token lleva el <c>usuarioId</c> de la persona y debe coincidir con la ruta: un token capturado no sirve para otra persona. Nunca se cachean.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/interno/cuentas/{usuarioId}")]
[Authorize(Policy = InternalServiceAuthentication.AccountPolicy)]
[PrivateNoStore]
internal sealed class InternalAccountController(AccountDataService service) : BusinessRuleController
{
    public const string PersonClaim = "usuarioId";
    public const int MaxPersonLength = 64;

    /// <summary>Citas y cotizaciones abiertas de la persona, con conteos e identificadores propios (máximo 50 por grupo).</summary>
    [HttpGet("pendientes")]
    [ProducesResponseType<AccountPendingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    public async Task<IActionResult> Pending(string usuarioId, CancellationToken cancellationToken) =>
        NotTheTokenPerson(usuarioId) ?? Ok(await service.PendingAsync(usuarioId, cancellationToken));

    /// <summary>Cancela las solicitudes abiertas y retira las cotizaciones pendientes de la persona, avisando a la otra parte.</summary>
    [HttpPost("cancelaciones")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    public async Task<IActionResult> CancelOpen(string usuarioId, CancellationToken cancellationToken)
    {
        if (NotTheTokenPerson(usuarioId) is { } refused)
        {
            return refused;
        }

        await service.CancelOpenAsync(usuarioId, cancellationToken);
        return NoContent();
    }

    /// <summary>Datos de la persona en Mercado para su exportación (un JSON).</summary>
    [HttpGet("exportacion")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    public async Task<IActionResult> Export(string usuarioId, CancellationToken cancellationToken)
    {
        if (NotTheTokenPerson(usuarioId) is { } refused)
        {
            return refused;
        }

        return Content(await service.ExportJsonAsync(usuarioId, cancellationToken), "application/json");
    }

    /// <summary>Consume <c>CuentaEliminada</c>: responde 202 cuando Mercado ya cerró y anonimizó lo de la persona (repetirlo no cambia nada).</summary>
    [HttpPost("eliminada")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    public async Task<IActionResult> AccountDeleted(string usuarioId, CancellationToken cancellationToken)
    {
        if (NotTheTokenPerson(usuarioId) is { } refused)
        {
            return refused;
        }

        await service.AccountDeletedAsync(usuarioId, cancellationToken);
        return Accepted();
    }

    private ObjectResult? NotTheTokenPerson(string usuarioId) =>
        usuarioId.Length is > 0 and <= MaxPersonLength && string.Equals(User.FindFirst(PersonClaim)?.Value, usuarioId, StringComparison.Ordinal)
            ? null
            : Rule(StatusCodes.Status403Forbidden, InternalServiceAuthentication.ServiceNotAllowedCode);
}
