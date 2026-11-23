using Knock.Market.Api.Modules.Requests.Data;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;

namespace Knock.Market.Api.Modules.Requests.Services;

/// <summary>Recibe los eventos de cuenta de Identidad (spec 059, R-10 y R-11) y delega en <see cref="AccountEventStore"/>. El cuerpo ya pasó el validador.</summary>
internal sealed class AccountEventService(AccountEventStore store)
{
    public Task<AccountEventOutcome> ReceiveAsync(AccountEventBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        return store.ApplyAsync(body.EventoId!, body.Tipo!, body.UsuarioId!, cancellationToken);
    }
}
