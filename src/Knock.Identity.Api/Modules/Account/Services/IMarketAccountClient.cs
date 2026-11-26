using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Account.Domain;

namespace Knock.Identity.Api.Modules.Account.Services;

/// <summary>
/// Consulta interna a Mercado para la cuenta (spec 039, R-24 y R-25): citas y cotizaciones abiertas de una persona y su cancelación al programar la
/// eliminación. La implementación real es <see cref="MarketAccountClient"/> (rutas internas con el token de servicio de la spec 037); sin dirección o sin clave, o si Mercado
/// falla, falla cerrado con 503: nunca finge que no hay pendientes.
/// </summary>
internal interface IMarketAccountClient
{
    /// <summary>Citas y cotizaciones abiertas de la persona del <paramref name="sub"/> y si tiene una visita en curso. Solo datos propios.</summary>
    Task<AccountPending> GetPendingAsync(string sub, CancellationToken cancellationToken);

    /// <summary>Cancela las solicitudes abiertas y las cotizaciones pendientes de la persona, avisando a la otra parte (R-25).</summary>
    Task CancelOpenAsync(string sub, CancellationToken cancellationToken);
}

/// <summary>Datos de la persona en Mercado para su exportación (spec 039, R-22): un texto JSON que Identidad guarda como el archivo de ese servicio en el ZIP.</summary>
internal interface IMarketExport
{
    Task<string> ExportJsonAsync(string sub, CancellationToken cancellationToken);
}

/// <summary>Sustituto que responde 503 <c>market_unavailable</c> en lugar de inventar un resultado; lo usan las pruebas y el cliente real cuando Mercado no responde.</summary>
internal sealed class UnavailableMarketAccountClient : IMarketAccountClient
{
    public Task<AccountPending> GetPendingAsync(string sub, CancellationToken cancellationToken) => throw Unavailable();

    public Task CancelOpenAsync(string sub, CancellationToken cancellationToken) => throw Unavailable();

    internal static DomainException Unavailable() =>
        new(DomainErrorKind.ServiceUnavailable, "The internal Market routes are not available.", AccountErrorCodes.MarketUnavailable);
}
