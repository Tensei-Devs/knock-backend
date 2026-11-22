using Knock.Contracts.Safety;
using Knock.Messaging.Api.Modules.Bloqueos.Data;
using Knock.Messaging.Api.Modules.Bloqueos.Dtos;

namespace Knock.Messaging.Api.Modules.Bloqueos.Services;

/// <summary>Aplica los eventos de bloqueo de Identidad a la proyección de Mensajería (spec 043, R-23). El cuerpo ya viene validado.</summary>
internal sealed class BloqueosService(IBloqueosProyeccion projection)
{
    public async Task ApplyAsync(EventoDeBloqueo body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var removed = body.Tipo == BlockRemoved.EventType;
        var when = DateTime.SpecifyKind(body.OcurridoEn!.Value, DateTimeKind.Utc);
        await projection.ApplyAsync(body.BloqueaId!, body.BloqueadoId!, !removed, when, body.EventoId!, cancellationToken);
    }
}
