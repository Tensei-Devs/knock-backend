using Knock.Messaging.Api.Modules.Chat;
using Microsoft.AspNetCore.SignalR;

namespace Knock.Messaging.Api.Modules.Conversaciones.Services;

/// <summary>Cuerpo del evento <c>noLeidos</c> (spec 030, §5): el contador de la conversación y el total de la persona. Sin texto, nombres ni <c>sub</c>.</summary>
internal sealed record NoLeidosEvent(string ConversacionId, int NoLeidos, int Total);

/// <summary>Entrega en vivo a quien está conectado por el hub de la spec 026 (R-14 y R-19). Siempre después de confirmar el cambio; si falla, el cambio guardado no se pierde.</summary>
internal interface INotificadorEnVivo
{
    /// <summary>Avisa a todos los dispositivos conectados de <paramref name="subject"/> que cambió el contador de una conversación.</summary>
    Task UnreadChangedAsync(string subject, string conversationId, int unread, int total, CancellationToken cancellationToken);
}

/// <summary>
/// Publica <c>noLeidos</c> al grupo <c>usuario:{sub}</c> del hub (R-15). Es un aviso de mejor esfuerzo: el contador ya quedó guardado y la app lo lee con <c>GET /conversaciones/no-leidas</c>,
/// así que un fallo del puente de Redis no convierte en error una lectura que ya se guardó.
/// </summary>
internal sealed class NotificadorEnVivo(IHubContext<ChatHub> hub) : INotificadorEnVivo
{
    public const string UserGroupPrefix = "usuario:";
    public const string UnreadEvent = "noLeidos";

    public async Task UnreadChangedAsync(string subject, string conversationId, int unread, int total, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        try
        {
            await hub.Clients.Group(UserGroupPrefix + subject).SendAsync(UnreadEvent, new NoLeidosEvent(conversationId, unread, total), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Aviso de mejor esfuerzo: el contador ya está guardado. Sin registrar la excepción, que puede traer el grupo (con el sub).
        }
    }
}
