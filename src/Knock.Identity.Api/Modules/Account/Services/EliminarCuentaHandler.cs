using Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;
using Knock.Identity.Api.Modules.Account.Data;
using Knock.Identity.Api.Modules.Account.Domain;
using Knock.Identity.Api.Modules.Sesiones.Services;
using Knock.Identity.Api.Modules.Trust;

namespace Knock.Identity.Api.Modules.Account.Services;

/// <summary>
/// Tarea <c>EliminarCuenta</c> (spec 039, R-25; spec 054, R-09). Al vencer la gracia: purga las fotos y llaves de verificación (spec 057), deja el evento <c>CuentaEliminada</c> para
/// Mercado, borra los datos personales y las direcciones y cierra las sesiones. Es idempotente: si la persona canceló, la cuenta ya está borrada o la gracia que programó la tarea no
/// es la vigente, termina sin efecto. Los registros no llevan identificadores de la persona.
/// </summary>
internal sealed partial class EliminarCuentaHandler(
    IAccountStore store,
    ITrustApi trust,
    IAccountOutbox outbox,
    ISessionCanceller cancelled,
    TimeProvider clock,
    ILogger<EliminarCuentaHandler> logger) : IManejadorDeTarea
{
    public string Tipo => AccountOutbox.DeletionTask;

    public async Task HandleAsync(TareaReservada task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        var sub = task.ClaveNegocio.Split(':')[0];
        var account = await store.FindBySubAsync(sub, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        if (account is not { State: AccountStates.DeletionScheduled, DeleteAt: { } deleteAt }
            || deleteAt > now
            || AccountOutbox.DeletionKey(sub, deleteAt) != task.ClaveNegocio)
        {
            LogNothingToDo(logger);
            return;
        }

        await trust.PurgeAccountAsync(sub, cancellationToken);
        await outbox.EnqueueAccountDeletedAsync(sub, cancellationToken);
        var revoked = await store.EraseAccountAsync(account.Id, now, cancellationToken);
        if (revoked is null)
        {
            LogNothingToDo(logger);
            return;
        }

        foreach (var sessionId in revoked)
        {
            await cancelled.CancelAsync(sessionId, SessionTokenIssuer.AccessTokenLifetime, cancellationToken);
        }

        LogDeleted(logger, revoked.Count);
    }

    [LoggerMessage(EventId = 3917, Level = LogLevel.Information, Message = "Account deletion task did nothing: cancelled, already deleted or superseded")]
    private static partial void LogNothingToDo(ILogger logger);

    [LoggerMessage(EventId = 3918, Level = LogLevel.Information, Message = "Account deleted after the grace period; {ClosedSessions} sessions closed")]
    private static partial void LogDeleted(ILogger logger, int closedSessions);
}
