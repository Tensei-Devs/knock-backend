using Knock.Messaging.Api.Modules.Avisos.Domain;

namespace Knock.Messaging.Api.Modules.Avisos.Push;

internal enum DispatchOutcome
{
    Disabled,
    Invalid,
    Duplicate,
    Present,
    Grouped,
    NoDevices,
    Sent,
    Failed,
    Limited,
}

/// <summary>Registro de envíos (<c>notifications.enviados</c>): índice único de deduplicación y resultado sin identificadores de recurso (R-06).</summary>
internal interface IEnviadosStore
{
    Task<bool> TryReserveAsync(string deduplicationKey, string evento, string tipo, CancellationToken cancellationToken);

    Task RecordResultAsync(string deduplicationKey, string result, CancellationToken cancellationToken);

    Task<DateTime?> GetWindowStartAsync(string conversation, string person, CancellationToken cancellationToken);

    /// <summary>Abre la ventana de 5 minutos de <c>mensaje_nuevo</c> de la conversación y la persona (R-07); la conversación se guarda solo como huella.</summary>
    Task OpenWindowAsync(string conversation, string person, DateTime nowUtc, CancellationToken cancellationToken);

    /// <summary>Cuenta un mensaje agrupado dentro de la ventana abierta, para el aviso único al cerrarla (R-07).</summary>
    Task CountSuppressedAsync(string conversation, string person, CancellationToken cancellationToken);

    /// <summary>Envíos de la persona desde <paramref name="sinceUtc"/> (límite por persona y hora, R-12).</summary>
    Task<int> CountRecentAsync(string person, DateTime sinceUtc, CancellationToken cancellationToken);
}

/// <summary>Presencia en vivo <c>chat:presencia:{sub}</c> (R-08).</summary>
internal interface IPresenceReader
{
    Task<bool> IsPresentAsync(string person, CancellationToken cancellationToken);
}

/// <summary>Identificadores de dispositivo descifrados de una persona; se borran si FCM los invalida (CA-08).</summary>
internal interface IDeviceTokens
{
    Task<IReadOnlyList<string>> ListAsync(string person, CancellationToken cancellationToken);

    Task DeleteAsync(string person, string deviceToken, CancellationToken cancellationToken);

    /// <summary>Borra los dispositivos ligados a la sesión que terminó (<c>sesion_revocada</c>, R-13).</summary>
    Task DeleteBySessionAsync(string session, CancellationToken cancellationToken);
}

internal sealed record AvisoEvent(string EventoId, string Evento, string Destinatario, string? Recurso, string? Conversacion);

/// <summary>Núcleo del despachador (spec 037 R-04 a R-09). No lee colecciones de otros servicios: recibe el evento ya entregado. Con el interruptor apagado no envía ni registra nada.</summary>
internal sealed class DespachadorDeAvisos(
    PushOptions options,
    IPushSender sender,
    IEnviadosStore sent,
    IPresenceReader presence,
    IDeviceTokens devices,
    TimeProvider clock)
{
    public const int MaxAttempts = 3;

    public async Task<DispatchOutcome> DispatchAsync(AvisoEvent evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        if (!options.Enabled)
        {
            return DispatchOutcome.Disabled;
        }

        var payload = AvisoRules.BuildPayload(evento.Evento, evento.Recurso, evento.Conversacion, Guid.NewGuid());
        if (payload is null)
        {
            return DispatchOutcome.Invalid;
        }

        if (evento.Evento == AvisoRules.NewMessageEvent)
        {
            if (await presence.IsPresentAsync(evento.Destinatario, cancellationToken))
            {
                return DispatchOutcome.Present;
            }

            var start = await sent.GetWindowStartAsync(evento.Conversacion!, evento.Destinatario, cancellationToken);
            if (AvisoRules.DecideWindow(start, clock.GetUtcNow().UtcDateTime) == WindowDecision.Suppress)
            {
                await sent.CountSuppressedAsync(evento.Conversacion!, evento.Destinatario, cancellationToken);
                return DispatchOutcome.Grouped;
            }
        }

        var key = AvisoRules.DeduplicationKey(evento.EventoId, evento.Destinatario);
        if (!await sent.TryReserveAsync(key, evento.Evento, payload["tipo"], cancellationToken))
        {
            return DispatchOutcome.Duplicate;
        }

        if (await sent.CountRecentAsync(evento.Destinatario, clock.GetUtcNow().UtcDateTime.AddHours(-1), cancellationToken) > options.HourlyLimit)
        {
            await sent.RecordResultAsync(key, "limitado", cancellationToken);
            return DispatchOutcome.Limited;
        }

        if (evento.Evento == AvisoRules.NewMessageEvent)
        {
            await sent.OpenWindowAsync(evento.Conversacion!, evento.Destinatario, clock.GetUtcNow().UtcDateTime, cancellationToken);
        }

        var tokens = await devices.ListAsync(evento.Destinatario, cancellationToken);
        if (tokens.Count == 0)
        {
            await sent.RecordResultAsync(key, "sin_dispositivos", cancellationToken);
            return DispatchOutcome.NoDevices;
        }

        var delivered = false;
        foreach (var token in tokens)
        {
            delivered |= await SendWithRetriesAsync(evento.Destinatario, token, payload, cancellationToken);
        }

        await sent.RecordResultAsync(key, delivered ? "enviado" : "fallido", cancellationToken);
        return delivered ? DispatchOutcome.Sent : DispatchOutcome.Failed;
    }

    private async Task<bool> SendWithRetriesAsync(string person, string token, IReadOnlyDictionary<string, string> payload, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var result = await sender.SendAsync(token, payload, cancellationToken);
            if (result == PushResult.Sent)
            {
                return true;
            }

            if (result == PushResult.InvalidToken)
            {
                await devices.DeleteAsync(person, token, cancellationToken);
                return false;
            }

            if (attempt < MaxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), clock, cancellationToken);
            }
        }

        return false;
    }
}
