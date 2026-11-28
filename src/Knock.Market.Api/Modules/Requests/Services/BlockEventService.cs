using Knock.Contracts.Safety;
using Knock.Market.Api.Modules.Requests.Data;
using Knock.Market.Api.Modules.Requests.Dtos;

namespace Knock.Market.Api.Modules.Requests.Services;

/// <summary>Aplica los eventos de bloqueo de Identidad a la proyección de Mercado (spec 043, R-23). El cuerpo ya viene validado.</summary>
internal sealed class BlockEventService(BlockProjectionStore projection)
{
    public Task ApplyAsync(BlockEventBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        return projection.ApplyAsync(body.BloqueaId!, body.BloqueadoId!, body.Tipo == BlockCreated.EventType, body.OcurridoEn!.Value.ToUniversalTime(), body.EventoId!, cancellationToken);
    }
}
