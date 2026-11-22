namespace Knock.Messaging.Api.Modules.Avisos.Push;

internal enum PushResult
{
    Sent,
    InvalidToken,
    Failed,
}

/// <summary>Envío de un mensaje solo de datos con prioridad alta a un dispositivo (spec 037 R-05). Detrás de este puerto vive FCM.</summary>
internal interface IPushSender
{
    Task<PushResult> SendAsync(string deviceToken, IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken);
}

/// <summary>Interruptor <c>Notificaciones:PushHabilitado</c>, apagado por omisión (R-10: sin credenciales no se envía nada).</summary>
internal sealed record PushOptions(bool Enabled)
{
    public const string EnabledKey = "Notificaciones:PushHabilitado";

    public static PushOptions Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new PushOptions(bool.TryParse(configuration[EnabledKey], out var value) && value);
    }
}

/// <summary>Implementación a prueba de fallos: no envía y reporta fallo; sirve mientras no haya credenciales de Firebase.</summary>
internal sealed class NoOpPushSender : IPushSender
{
    public Task<PushResult> SendAsync(string deviceToken, IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken) =>
        Task.FromResult(PushResult.Failed);
}
