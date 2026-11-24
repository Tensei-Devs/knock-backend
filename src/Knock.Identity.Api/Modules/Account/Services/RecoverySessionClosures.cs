using Knock.Identity.Api.Modules.Moderation.Services;
using Knock.Identity.Api.Modules.Sesiones.Services;

namespace Knock.Identity.Api.Modules.Account.Services;

/// <summary>
/// Lo que sigue de aprobar una recuperación de acceso desde el panel (spec 053, D-07; spec 039, R-08): deja el aviso <c>sesion_revocada</c> de cada sesión cerrada (spec 037, R-13) y la cancela en la lista de Redis
/// que lee el gateway (spec 016). Ambos pasos son idempotentes; ninguno lleva datos personales.
/// </summary>
internal sealed class RecoverySessionClosures(IAccountOutbox outbox, ISessionCanceller canceller) : IRecoverySessionClosures
{
    public async Task AfterCommitAsync(IReadOnlyList<string> sessionIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionIds);
        foreach (var sessionId in sessionIds)
        {
            await outbox.EnqueueSessionRevokedAsync(sessionId, cancellationToken);
            await canceller.CancelAsync(sessionId, SessionTokenIssuer.AccessTokenLifetime, cancellationToken);
        }
    }
}
