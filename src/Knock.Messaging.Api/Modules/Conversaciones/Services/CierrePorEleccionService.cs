using Knock.Messaging.Api.Modules.Conversaciones.Data;

namespace Knock.Messaging.Api.Modules.Conversaciones.Services;

/// <summary>Cierra las demás conversaciones de una solicitud cuando Mercado reporta la cotización elegida (spec 048, CA-21). Es idempotente: solo toca las que siguen abiertas.</summary>
internal sealed class CierrePorEleccionService(ConversacionesStore conversations, TimeProvider clock)
{
    public Task CloseOthersAsync(string requestId, string chosenTechnicianUserId, CancellationToken cancellationToken) =>
        conversations.CloseOthersAsync(requestId, chosenTechnicianUserId, clock.GetUtcNow().UtcDateTime, cancellationToken);
}
