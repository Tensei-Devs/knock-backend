namespace Knock.Identity.Api.Modules.Trust.Services;

/// <summary>
/// Salida de eventos de la verificación (spec 025, §8): <c>VerificacionEnviada</c> y <c>CurpDuplicada</c>. Se escriben en el buzón de salida (spec 037/054), que todavía no existe:
/// mientras tanto <see cref="PendingVerificationEvents"/> solo deja constancia sin datos. Los eventos no llevan CURP, HMAC ni fotos.
/// </summary>
internal interface IVerificationEvents
{
    Task PublishAsync(string eventType, string sub, string? verificationId, CancellationToken cancellationToken);
}

/// <summary>Sustituto mientras no existe el buzón de salida: registra que el evento quedó pendiente, sin ids ni datos de la persona.</summary>
internal sealed partial class PendingVerificationEvents(ILogger<PendingVerificationEvents> logger) : IVerificationEvents
{
    public Task PublishAsync(string eventType, string sub, string? verificationId, CancellationToken cancellationToken)
    {
        LogPending(logger, eventType);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 3991, Level = LogLevel.Warning, Message = "Verification event {EventType} not delivered: the outbox (spec 054) is not implemented yet")]
    private static partial void LogPending(ILogger logger, string eventType);
}
