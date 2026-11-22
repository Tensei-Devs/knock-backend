using Knock.Contracts.Conversations;
using Knock.Contracts.Moderation;
using Knock.Messaging.Api.Common.Errors;
using Knock.Messaging.Api.Modules.Conversaciones.Data;
using Knock.Messaging.Api.Modules.Conversaciones.Domain;
using Knock.Messaging.Api.Modules.Conversaciones.Dtos;

namespace Knock.Messaging.Api.Modules.Conversaciones.Services;

/// <summary>
/// Enviar un mensaje a una conversación (spec 030, R-08 a R-14), en este orden: participante, permiso vigente (Mercado o, si no contesta, la caché de 60 s), conversación abierta, ocultamiento
/// de contacto, moderación que falla cerrada, guardado transaccional y entrega en vivo. El texto ya llega validado (R-10) y el límite de 30 por minuto lo cobra el limitador antes de llegar
/// aquí (R-11). Nunca registra el texto, el original ocultado, el <c>sub</c> ni identificadores.
/// </summary>
internal sealed class EnvioDeMensajeService(
    ConversacionesStore conversations,
    MensajesStore messages,
    IPermisoDeConversacion permissions,
    IContentModerator moderator,
    INotificadorEnVivo live,
    TimeProvider clock)
{
    public const string ContentRejectedCode = "content_rejected";
    public const string ModerationUnavailableCode = "moderation_unavailable";

    public async Task<MensajeView> SendAsync(string subject, string bearerToken, string conversationId, string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(bearerToken);
        ArgumentNullException.ThrowIfNull(text);

        var id = ConversacionErrors.ParseId(conversationId);
        var conversation = await conversations.FindForParticipantAsync(id, subject, cancellationToken) ?? throw ConversacionErrors.NotFound();
        var role = conversation.RoleOf(subject) ?? throw ConversacionErrors.NotFound();
        var permission = await PermissionAsync(conversation, role, subject, bearerToken, cancellationToken);
        if (!permission.Abierta)
        {
            throw new DomainException(
                DomainErrorKind.Conflict,
                "The conversation is closed.",
                ConversacionErrors.ConversationClosedCode,
                permission.MotivoCierre is null ? null : new Dictionary<string, string> { ["motivoCierre"] = permission.MotivoCierre });
        }

        var hidden = permission.Elegido ? new TextoOculto(text, false) : OcultadorDeContacto.Ocultar(text);
        await ModerateAsync(hidden.Texto, cancellationToken);

        var now = clock.GetUtcNow();
        var message = new NuevoMensaje(id, Guid.CreateVersion7(now).ToString("D"), role, subject, hidden.Texto, hidden.Oculto, now.UtcDateTime);
        var saved = await messages.SaveAsync(message, cancellationToken);
        var view = new MensajeView(message.MensajeId, role, hidden.Texto, hidden.Oculto, message.CreadoEnUtc);
        await live.MessageAddedAsync(conversationId, view, cancellationToken);
        await live.UnreadChangedAsync(saved.DestinatarioUsuarioId, conversationId, saved.NoLeidos, saved.TotalNoLeidos, cancellationToken);
        return view;
    }

    private async Task<PermisoConversacion> PermissionAsync(StoredConversation conversation, string role, string subject, string bearerToken, CancellationToken cancellationToken)
    {
        // El técnico no manda tecnicoId: Mercado lo resuelve por su sub (R-02).
        var answer = await permissions.ConsultarAsync(
            conversation.SolicitudId,
            role == ConversacionRoles.Cliente ? conversation.TecnicoId : null,
            conversation.Id.ToString(),
            subject,
            bearerToken,
            cancellationToken);
        switch (answer)
        {
            case { Estado: EstadoPermisoConversacion.Concedido, Permiso: { } granted }:
                if (Differs(conversation, granted))
                {
                    await conversations.RefreshPermissionAsync(conversation.Id, granted, clock.GetUtcNow().UtcDateTime, cancellationToken);
                }

                return granted;
            case { Estado: EstadoPermisoConversacion.NoEncontrado }:
                throw ConversacionErrors.NotFound();
            default:
                throw new DomainException(DomainErrorKind.ServiceUnavailable, "The market service did not answer.", ErrorCodes.MarketUnavailableCode);
        }
    }

    private static bool Differs(StoredConversation stored, PermisoConversacion permission) =>
        stored.Estado != (permission.Abierta ? ConversacionEstados.Abierta : ConversacionEstados.Cerrada)
        || stored.MotivoCierre != permission.MotivoCierre
        || stored.Elegido != permission.Elegido
        || stored.VisitaId != permission.VisitaId;

    /// <summary>La moderación falla cerrada (constitución §2.13): rechazo es 422 y cualquier otra cosa que no sea «permitido» (incluida una excepción del moderador) es 503.</summary>
    private async Task ModerateAsync(string text, CancellationToken cancellationToken)
    {
        ModerationResult result;
        try
        {
            result = await moderator.ReviewTextAsync(text, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            result = new ModerationResult(ModerationOutcome.Unavailable);
        }

        switch (result.Outcome)
        {
            case ModerationOutcome.Allowed:
                return;
            case ModerationOutcome.Rejected:
                throw new DomainException(DomainErrorKind.Unprocessable, "The message was rejected by moderation.", ContentRejectedCode);
            default:
                throw new DomainException(DomainErrorKind.ServiceUnavailable, "Moderation is not available.", ModerationUnavailableCode);
        }
    }
}
