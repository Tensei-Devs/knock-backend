using Knock.Messaging.Api.Modules.Conversaciones.Data;

namespace Knock.Messaging.Api.Modules.Conversaciones.Services;

/// <summary>
/// Quién puede unirse al grupo <c>conversacion:{id}</c> del hub (spec 030, R-15): solo un participante, comprobado contra la conversación guardada y sin llamar a Mercado. Un identificador
/// mal formado, una conversación que no existe y una ajena dan el mismo resultado (<see langword="false"/>), para no revelar que existe (CA-03).
/// </summary>
internal sealed class AccesoAConversacionService(IMensajesStore store)
{
    public async Task<bool> PuedeUnirseAsync(string? sub, string? conversacionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(sub) || string.IsNullOrEmpty(conversacionId))
        {
            return false;
        }

        var conversation = await store.ObtenerAsync(conversacionId, cancellationToken);
        return conversation?.RolDe(sub) is not null;
    }
}
