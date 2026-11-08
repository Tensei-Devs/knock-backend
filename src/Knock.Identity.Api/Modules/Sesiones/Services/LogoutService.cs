using Knock.Identity.Api.Modules.Sesiones.Data;

namespace Knock.Identity.Api.Modules.Sesiones.Services;

/// <summary>
/// Cierre de la sesión del dispositivo (spec 016, R-06): revoca la sesión en MongoDB y cancela su <c>sid</c> en Redis con el resto de vigencia
/// del JWT, para que el gateway lo rechace aunque no haya vencido.
/// </summary>
internal sealed partial class LogoutService(ISesionesStore store, ISesionCancelada cancelled, ILogger<LogoutService> logger)
{
    /// <summary>Vigencia máxima de la cancelación: la del JWT de acceso.</summary>
    public static readonly TimeSpan MaxCancellation = SessionTokenIssuer.AccessTokenLifetime;

    private static readonly TimeSpan _minCancellation = TimeSpan.FromSeconds(1);

    /// <param name="sessionId"><c>sid</c> del JWT.</param>
    /// <param name="sub"><c>sub</c> del JWT: solo se cierra una sesión de la propia cuenta.</param>
    /// <param name="remainingLifetime">Lo que le queda al JWT presentado (<c>exp</c> menos ahora).</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    public async Task LogoutAsync(string sessionId, string sub, TimeSpan remainingLifetime, CancellationToken cancellationToken)
    {
        // Solo se cancela en Redis una sesión que de verdad es de esta cuenta: con otro sid el gateway cancelaría la sesión de otra persona.
        if (!await store.RevokeAsync(sessionId, sub, cancellationToken))
        {
            LogIgnored(logger);
            return;
        }

        await cancelled.CancelAsync(sessionId, Clamp(remainingLifetime), cancellationToken);
        LogClosed(logger);
    }

    private static TimeSpan Clamp(TimeSpan lifetime)
    {
        if (lifetime < _minCancellation)
        {
            return _minCancellation;
        }

        return lifetime > MaxCancellation ? MaxCancellation : lifetime;
    }

    [LoggerMessage(EventId = 2111, Level = LogLevel.Information, Message = "Session closed")]
    private static partial void LogClosed(ILogger logger);

    [LoggerMessage(EventId = 2112, Level = LogLevel.Warning, Message = "Logout ignored: the session does not belong to the account")]
    private static partial void LogIgnored(ILogger logger);
}
