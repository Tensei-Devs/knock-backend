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
internal sealed record PushOptions(bool Enabled, int HourlyLimit = PushOptions.DefaultHourlyLimit)
{
    public const string EnabledKey = "Notificaciones:PushHabilitado";
    public const string HourlyLimitKey = "Avisos:LimitePorPersonaHora";
    public const int DefaultHourlyLimit = 30;
    public const int MaxHourlyLimit = 1000;

    /// <summary>Lee el interruptor y el límite por persona y hora (R-12). Un límite fuera de 1 a 1000 impide arrancar; sin valor se usa 30.</summary>
    public static PushOptions Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var enabled = bool.TryParse(configuration[EnabledKey], out var value) && value;
        var raw = configuration[HourlyLimitKey];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new PushOptions(enabled);
        }

        if (!int.TryParse(raw, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var limit) || limit is < 1 or > MaxHourlyLimit)
        {
            throw new InvalidOperationException($"{HourlyLimitKey} must be an integer from 1 to {MaxHourlyLimit}.");
        }

        return new PushOptions(enabled, limit);
    }
}

/// <summary>Implementación a prueba de fallos: no envía y reporta fallo; sirve mientras no haya credenciales de Firebase.</summary>
internal sealed class NoOpPushSender : IPushSender
{
    public Task<PushResult> SendAsync(string deviceToken, IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken) =>
        Task.FromResult(PushResult.Failed);
}
