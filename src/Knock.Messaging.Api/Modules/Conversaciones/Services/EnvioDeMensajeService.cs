using Knock.Contracts.Conversations;
using Knock.Contracts.Moderation;
using Knock.Messaging.Api.Modules.Bloqueos;
using Knock.Messaging.Api.Modules.Conversaciones.Data;
using Knock.Messaging.Api.Modules.Conversaciones.Domain;
using Knock.Messaging.Api.Modules.Conversaciones.Dtos;

namespace Knock.Messaging.Api.Modules.Conversaciones.Services;

/// <summary>
/// Enviar un mensaje a una conversación (spec 030, R-08 a R-14), en este orden: participante, sin bloqueo entre ambas, permiso vigente (caché de 60 s o Mercado), conversación abierta, ocultamiento de contacto,
/// moderación que falla cerrada, guardado transaccional y entrega en vivo. El texto ya llega validado (R-10) y el límite de 30 por minuto lo cobra el limitador antes de llegar aquí (R-11).
/// Nunca registra el texto, el original ocultado, el <c>sub</c> ni identificadores: solo códigos.
/// </summary>
internal sealed class EnvioDeMensajeService(
    IMensajesStore store,
    IBloqueosApi blocks,
    IPermisoDeConversacion permisos,
    IContentModerator moderator,
    INotificadorEnVivo vivo,
    TimeProvider clock,
    ILogger<EnvioDeMensajeService> logger)
{
    private static readonly Action<ILogger, Exception?> _liveDeliveryFailed = LoggerMessage.Define(LogLevel.Warning, new EventId(301, "ConversationLiveDeliveryFailed"), "Live delivery of a saved message failed; the mailbox event remains.");

    public async Task<MensajeResponse> EnviarAsync(string sub, string bearerToken, string conversacionId, string texto, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);
        ArgumentException.ThrowIfNullOrWhiteSpace(bearerToken);
        var clean = ReglasDeMensaje.Limpiar(texto) ?? throw new ArgumentException("The message text is not valid.", nameof(texto));

        var conversation = await store.ObtenerAsync(conversacionId, cancellationToken);
        var role = conversation?.RolDe(sub);
        if (conversation is null || role is null)
        {
            throw NotFound();
        }

        // Un bloqueo en cualquier sentido corta el envío y responde igual que una conversación que no existe: no se revela el bloqueo (spec 043, R-16, R-17, CA-26, CA-27).
        if (await blocks.AreBlockedAsync(conversation.ClienteId, conversation.TecnicoUsuarioId, cancellationToken))
        {
            throw NotFound();
        }

        var permission = await PermisoAsync(conversation, role, sub, bearerToken, cancellationToken);
        if (!permission.Abierta)
        {
            throw new DomainException(
                DomainErrorKind.Conflict,
                "The conversation is closed.",
                CodigosDeConversacion.ConversationClosedCode,
                permission.MotivoCierre is null ? null : new Dictionary<string, string> { ["motivoCierre"] = permission.MotivoCierre });
        }

        var hidden = permission.Elegido ? new TextoOculto(clean, false) : OcultadorDeContacto.Ocultar(clean);
        await ModerarAsync(hidden.Texto, cancellationToken);

        var now = clock.GetUtcNow();
        var message = new NuevoMensaje(conversacionId, Guid.CreateVersion7(now).ToString("D"), role, sub, hidden.Texto, hidden.Oculto, now.UtcDateTime);
        var saved = await store.GuardarAsync(message, cancellationToken);
        var response = new MensajeResponse(message.MensajeId, role, hidden.Texto, hidden.Oculto, now);
        await EntregarAsync(conversacionId, response, saved, cancellationToken);
        return response;
    }

    private static DomainException NotFound() => new(DomainErrorKind.NotFound, "Conversation not found.", CodigosDeConversacion.NotFoundCode);

    private async Task<PermisoVigente> PermisoAsync(ConversacionParaEnvio conversation, string role, string sub, string bearerToken, CancellationToken cancellationToken)
    {
        // El técnico no manda tecnicoId: Mercado lo resuelve por su sub (R-02). El cliente de permiso guarda el permiso concedido 60 s (R-09) y responde con él si Mercado no contesta.
        var asked = await permisos.ConsultarAsync(
            conversation.SolicitudId,
            string.Equals(role, ReglasDeMensaje.RolCliente, StringComparison.Ordinal) ? conversation.TecnicoId : null,
            conversation.Id,
            sub,
            bearerToken,
            cancellationToken);
        switch (asked)
        {
            case { Estado: EstadoPermisoConversacion.Concedido, Permiso: { } granted }:
                var current = new PermisoVigente(granted.Abierta, granted.MotivoCierre, granted.Elegido, granted.VisitaId);
                await store.GuardarPermisoAsync(conversation.Id, current, clock.GetUtcNow().UtcDateTime, cancellationToken);
                return current;
            case { Estado: EstadoPermisoConversacion.NoEncontrado }:
                throw NotFound();
            default:
                throw new DomainException(DomainErrorKind.ServiceUnavailable, "Market did not answer.", CodigosDeConversacion.MarketUnavailableCode);
        }
    }

    /// <summary>La moderación falla cerrada (constitución §2.13): rechazo es 422 y cualquier otra cosa que no sea «permitido» (incluida una excepción del moderador) es 503.</summary>
    private async Task ModerarAsync(string texto, CancellationToken cancellationToken)
    {
        ModerationResult result;
        try
        {
            result = await moderator.ReviewTextAsync(texto, cancellationToken);
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
                throw new DomainException(DomainErrorKind.Unprocessable, "The message was rejected by moderation.", CodigosDeConversacion.ContentRejectedCode);
            default:
                throw new DomainException(DomainErrorKind.ServiceUnavailable, "Moderation is not available.", CodigosDeConversacion.ModerationUnavailableCode);
        }
    }

    /// <summary>Después de confirmar: el mensaje al grupo de la conversación y el contador a la otra parte. Si falla, el mensaje ya está guardado y el buzón avisará; no se pierde ni se reintenta el guardado.</summary>
    private async Task EntregarAsync(string conversacionId, MensajeResponse response, MensajeGuardado saved, CancellationToken cancellationToken)
    {
        try
        {
            await vivo.NewMessageAsync(conversacionId, response, cancellationToken);
            await vivo.UnreadChangedAsync(saved.DestinatarioUsuarioId, conversacionId, saved.NoLeidos, saved.TotalNoLeidos, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _liveDeliveryFailed(logger, null);
        }
    }
}
