using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Sesiones.Data;
using Knock.Identity.Api.Modules.Sesiones.Domain;
using Knock.Identity.Api.Modules.Sesiones.Dtos;
using MongoDB.Bson;

namespace Knock.Identity.Api.Modules.Sesiones.Services;

internal sealed partial class SessionManagementService(
    ISesionesStore store,
    ISesionCancelada cancelled,
    TimeProvider timeProvider,
    ILogger<SessionManagementService> logger)
{
    public async Task<IReadOnlyList<SessionListItemResponse>> ListOpenSessionsAsync(
        string sub,
        string currentSessionId,
        CancellationToken cancellationToken)
    {
        var account = await store.FindBySubAsync(sub, cancellationToken);
        if (account is null)
        {
            return [];
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var sessions = await store.FindOpenSessionsAsync(account.Id, now, cancellationToken);
        return sessions
            .Select(session => new SessionListItemResponse(
                session.Id,
                session.DeviceName,
                session.LastActivity,
                string.Equals(session.Id, currentSessionId, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    public async Task RevokeSessionAsync(
        string sub,
        string currentSessionId,
        string requestedSessionId,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(requestedSessionId, out var sessionId))
        {
            throw new DomainException(DomainErrorKind.BadRequest, "The session id is invalid.", "invalid_session_id");
        }

        if (ObjectId.TryParse(currentSessionId, out var currentId) && sessionId == currentId)
        {
            throw new DomainException(
                DomainErrorKind.Conflict,
                "The current session must be closed through the logout endpoint.",
                "current_session");
        }

        var account = await store.FindBySubAsync(sub, cancellationToken);
        var session = account is null
            ? null
            : await store.FindSessionByOwnerAsync(sessionId.ToString(), account.Id, cancellationToken);
        if (account is null || session is null)
        {
            LogRevocationTargetNotFound(logger);
            throw new DomainException(DomainErrorKind.NotFound, "The session was not found.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var remainingLifetime = session.LastActivity + SessionTokenIssuer.AccessTokenLifetime - now;
        if (remainingLifetime > SessionTokenIssuer.AccessTokenLifetime)
        {
            remainingLifetime = SessionTokenIssuer.AccessTokenLifetime;
        }

        if (remainingLifetime > TimeSpan.Zero)
        {
            await cancelled.CancelAsync(session.Id, remainingLifetime, cancellationToken);
        }

        if (!await store.MarkSessionRevokedAsync(session.Id, account.Id, cancellationToken))
        {
            LogRevocationTargetNotFound(logger);
            throw new DomainException(DomainErrorKind.NotFound, "The session was not found.");
        }
    }

    [LoggerMessage(EventId = 21801, Level = LogLevel.Warning, Message = "Session revocation target was not found or is not owned by the caller")]
    private static partial void LogRevocationTargetNotFound(ILogger logger);
}
