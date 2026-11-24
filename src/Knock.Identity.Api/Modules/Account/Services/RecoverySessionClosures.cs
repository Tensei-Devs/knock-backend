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
        // Un fallo en una sesión no deja sin cancelar a las demás: se intentan todas y, si alguna falla, se avisa al final para que la petición no responda éxito (fallar cerrado).
        var failures = new List<Exception>();
        foreach (var sessionId in sessionIds)
        {
            // El aviso va antes de cancelar (spec 037, R-13), pero que falle no impide cancelar la sesión.
            await TryAsync(() => outbox.EnqueueSessionRevokedAsync(sessionId, cancellationToken), failures);
            await TryAsync(() => canceller.CancelAsync(sessionId, SessionTokenIssuer.AccessTokenLifetime, cancellationToken), failures);
        }

        ThrowIfAny(failures);
    }

    private static async Task TryAsync(Func<Task> step, List<Exception> failures)
    {
        try
        {
            await step();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            failures.Add(exception);
        }
    }

    public async Task RetryCancellationAsync(IReadOnlyList<string> sessionIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionIds);
        var failures = new List<Exception>();
        foreach (var sessionId in sessionIds)
        {
            await TryAsync(() => canceller.CancelAsync(sessionId, SessionTokenIssuer.AccessTokenLifetime, cancellationToken), failures);
        }

        ThrowIfAny(failures);
    }

    private static void ThrowIfAny(List<Exception> failures)
    {
        if (failures.Count > 0)
        {
            throw new AggregateException("The sessions of an approved recovery could not all be cancelled.", failures);
        }
    }
}
