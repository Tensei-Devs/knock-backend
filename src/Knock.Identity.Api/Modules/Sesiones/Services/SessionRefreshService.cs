using Knock.Identity.Api.Modules.Sesiones.Data;
using Knock.Identity.Api.Modules.Sesiones.Domain;

namespace Knock.Identity.Api.Modules.Sesiones.Services;

/// <summary>Par emitido al renovar: el JWT nuevo (mismo <c>sid</c>), su duración en segundos y el token de renovación nuevo.</summary>
internal sealed record RefreshResult(string AccessToken, int ExpiresInSeconds, string RefreshToken);

/// <summary>
/// Renovación de la sesión (spec 016, R-02 a R-05, R-08): rota el token de renovación en una sola operación y, si se presenta uno ya usado, lo trata
/// como robado y revoca todas las sesiones de la cuenta. Los rechazos devuelven <c>null</c>: el 401 no dice el motivo (R-10).
/// </summary>
internal sealed partial class SessionRefreshService(
    ISesionesStore store,
    ISesionCancelada cancelled,
    SessionTokenIssuer issuer,
    TimeProvider timeProvider,
    ILogger<SessionRefreshService> logger)
{
    public async Task<RefreshResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (!RefreshTokenFingerprint.TryCompute(refreshToken, out var fingerprint))
        {
            LogRejected(logger);
            return null;
        }

        var session = await store.FindSessionByFingerprintAsync(fingerprint, cancellationToken);
        if (session is null)
        {
            await RevokeAllIfReusedAsync(fingerprint, cancellationToken);
            LogRejected(logger);
            return null;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var account = await store.FindByIdAsync(session.UserId, cancellationToken);
        if (session.Revoked || session.ExpiresAt <= now || account is null || account.State == AccountState.Suspended)
        {
            LogRejected(logger);
            return null;
        }

        var issued = issuer.Issue(Guid.Parse(session.Sub), session.Id);
        var rotated = await store.RotateAsync(
            session.Id, fingerprint, RefreshTokenFingerprint.Compute(issued.RefreshToken), now, now + NewSession.Lifetime, cancellationToken);
        if (!rotated)
        {
            // Otra petición con el mismo token ganó la rotación (R-04), o la sesión se revocó entre la lectura y la escritura.
            LogRejected(logger);
            return null;
        }

        LogRotated(logger);
        return new RefreshResult(issued.AccessToken, (int)SessionTokenIssuer.AccessTokenLifetime.TotalSeconds, issued.RefreshToken);
    }

    private async Task RevokeAllIfReusedAsync(byte[] fingerprint, CancellationToken cancellationToken)
    {
        var reused = await store.FindSessionByUsedFingerprintAsync(fingerprint, cancellationToken);
        if (reused is null)
        {
            return;
        }

        var revoked = await store.RevokeAllAsync(reused.UserId, cancellationToken);
        foreach (var sessionId in revoked)
        {
            await cancelled.CancelAsync(sessionId, SessionTokenIssuer.AccessTokenLifetime, cancellationToken);
        }

        LogReuse(logger, revoked.Count);
    }

    // Los registros no llevan tokens, huellas, sid ni sub.
    [LoggerMessage(EventId = 2101, Level = LogLevel.Warning, Message = "Session refresh rejected")]
    private static partial void LogRejected(ILogger logger);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Information, Message = "Session refresh rotated the token")]
    private static partial void LogRotated(ILogger logger);

    [LoggerMessage(EventId = 2103, Level = LogLevel.Warning, Message = "A used refresh token was presented: {Revoked} sessions revoked")]
    private static partial void LogReuse(ILogger logger, int revoked);
}
