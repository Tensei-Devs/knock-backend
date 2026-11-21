using Knock.Messaging.Api.Modules.Chat;
using Knock.Messaging.Api.Modules.Conversaciones.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace Knock.Messaging.Api.Modules.Conversaciones.Services;

/// <summary>Cuerpo del evento <c>mensajeConversacion</c> (spec 030, §5): el mensaje con su conversación, sin <c>sub</c> ni nombre de quien lo escribió.</summary>
internal sealed record MensajeConversacionEvento(string ConversacionId, string MensajeId, string AutorRol, string Texto, bool ContactoOculto, DateTimeOffset EnviadoEn);

/// <summary>Cuerpo del evento <c>noLeidos</c>: el contador de la conversación y el total de la persona.</summary>
internal sealed record NoLeidosEvento(string ConversacionId, int NoLeidos, int Total);

/// <summary>Entrega en vivo por el hub de la spec 026 (spec 030, R-14). Solo entrega: lo que se publica ya está guardado.</summary>
internal interface INotificadorEnVivo
{
    Task MensajeNuevoAsync(string conversacionId, MensajeResponse mensaje, CancellationToken cancellationToken);

    Task NoLeidosAsync(string destinatarioUsuarioId, NoLeidosEvento evento, CancellationToken cancellationToken);
}

/// <summary>Publica por <see cref="IHubContext{THub}"/> a los grupos <c>conversacion:{id}</c> y <c>usuario:{sub}</c>.</summary>
internal sealed class NotificadorEnVivo(IHubContext<ChatHub> hub) : INotificadorEnVivo
{
    public const string MensajeEvent = "mensajeConversacion";
    public const string NoLeidosEvent = "noLeidos";

    public static string ConversationGroup(string conversacionId) => $"conversacion:{conversacionId}";

    public static string UserGroup(string sub) => $"usuario:{sub}";

    public Task MensajeNuevoAsync(string conversacionId, MensajeResponse mensaje, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        var evento = new MensajeConversacionEvento(conversacionId, mensaje.MensajeId, mensaje.AutorRol, mensaje.Texto, mensaje.ContactoOculto, mensaje.EnviadoEn);
        return hub.Clients.Group(ConversationGroup(conversacionId)).SendAsync(MensajeEvent, evento, cancellationToken);
    }

    public Task NoLeidosAsync(string destinatarioUsuarioId, NoLeidosEvento evento, CancellationToken cancellationToken) =>
        hub.Clients.Group(UserGroup(destinatarioUsuarioId)).SendAsync(NoLeidosEvent, evento, cancellationToken);
}
