using Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;
using Knock.Identity.Api.Modules.Account.Data;

namespace Knock.Identity.Api.Modules.Account.Services;

/// <summary>
/// Tarea <c>VencerRecuperacion</c> (spec 039, R-21; spec 054, R-09). A los 7 días de abierto, un caso de recuperación de acceso que nadie decidió pasa a <c>vencida</c> y deja de aparecer en la cola
/// del panel (spec 053). Es idempotente: si el caso ya se aprobó o rechazó, o ya venció, no cambia nada. La clave de la tarea es <c>{usuarioId}:{creado en segundos Unix}</c>, solo identificadores.
/// </summary>
internal sealed partial class VencerRecuperacionHandler(IAccountStore store, TimeProvider clock, ILogger<VencerRecuperacionHandler> logger) : IManejadorDeTarea
{
    public string Tipo => AccountOutbox.RecoveryExpiryTask;

    public async Task HandleAsync(TareaReservada task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        var parts = task.ClaveNegocio.Split(':');
        if (parts.Length != 2 || !long.TryParse(parts[1], out var createdAtSeconds))
        {
            LogBadKey(logger);
            return;
        }

        var expired = await store.ExpireRecoveryAsync(parts[0], createdAtSeconds, clock.GetUtcNow().UtcDateTime, cancellationToken);
        LogDone(logger, expired);
    }

    [LoggerMessage(EventId = 3922, Level = LogLevel.Warning, Message = "Recovery expiry task ignored: the business key is not valid")]
    private static partial void LogBadKey(ILogger logger);

    [LoggerMessage(EventId = 3923, Level = LogLevel.Information, Message = "Recovery expiry task ran; expired a case: {Expired}")]
    private static partial void LogDone(ILogger logger, bool expired);
}
