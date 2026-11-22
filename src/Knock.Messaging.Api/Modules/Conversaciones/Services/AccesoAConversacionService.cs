using Knock.Messaging.Api.Modules.Conversaciones.Data;
using MongoDB.Bson;

namespace Knock.Messaging.Api.Modules.Conversaciones.Services;

/// <summary>
/// Quién puede unirse al grupo <c>conversacion:{id}</c> del hub (spec 030, R-15): solo un participante, comprobado contra la conversación guardada y sin llamar a Mercado. Un identificador
/// mal formado, una conversación que no existe y una ajena dan el mismo resultado (<see langword="false"/>), para no revelar que existe (CA-03).
/// </summary>
internal sealed class AccesoAConversacionService(ConversacionesStore store)
{
    public async Task<bool> CanJoinAsync(string? subject, string? conversationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(subject) || conversationId is not { Length: 24 } || !ObjectId.TryParse(conversationId, out var id))
        {
            return false;
        }

        return await store.FindForParticipantAsync(id, subject, cancellationToken) is not null;
    }
}
