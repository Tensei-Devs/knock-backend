using Knock.Contracts.Safety;
using Knock.Messaging.Api.Modules.Bloqueos.Data;
using Knock.Messaging.Api.Modules.Bloqueos.Dtos;
using Knock.Messaging.Api.Modules.Conversaciones;

namespace Knock.Messaging.Api.Modules.Bloqueos.Services;

/// <summary>Aplica los eventos de bloqueo de Identidad a la proyección de Mensajería (spec 043, R-23) y, al crearse un bloqueo, deja de solo lectura la conversación abierta de la pareja (R-18). El cuerpo ya viene validado.</summary>
internal sealed class BloqueosService(IBloqueosProyeccion projection, IConversacionesApi conversations, TimeProvider clock)
{
    public async Task ApplyAsync(EventoDeBloqueo body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var removed = body.Tipo == BlockRemoved.EventType;
        var when = DateTime.SpecifyKind(body.OcurridoEn!.Value, DateTimeKind.Utc);
        await projection.ApplyAsync(body.BloqueaId!, body.BloqueadoId!, !removed, when, body.EventoId!, cancellationToken);
        if (!removed)
        {
            // «dismissed» ya es un motivo de cierre de Mercado: la persona bloqueada no ve un cierre que delate el bloqueo. Repetir el evento no cambia nada (solo cierra las abiertas).
            await conversations.CloseForBlockAsync(body.BloqueaId!, body.BloqueadoId!, clock.GetUtcNow().UtcDateTime, cancellationToken);
        }
    }
}
