using Knock.Market.Api.Modules.Requests.Data;
using MongoDB.Bson;

namespace Knock.Market.Api.Modules.Requests.Services;

internal sealed class CierreDeSolicitudService(CierreDeSolicitudStore store, TimeProvider clock)
{
    /// <summary>
    /// Cancela la solicitud del cliente. Si la actualización condicionada pierde la carrera contra otra operación (por ejemplo una elección que confirmó primero), se lee de
    /// nuevo una vez para responder con el estado real (spec 029, R-06).
    /// </summary>
    public async Task<CierreResult> CancelAsync(ObjectId id, string clientId, string? reason, CancellationToken cancellationToken)
    {
        var result = await store.CloseAsync(id, clientId, reason, false, clock.GetUtcNow().UtcDateTime, cancellationToken);
        return result.Status == "unchanged"
            ? await store.CloseAsync(id, clientId, reason, false, clock.GetUtcNow().UtcDateTime, cancellationToken)
            : result;
    }

    public Task<CierreResult> ExpireAsync(ObjectId id, CancellationToken cancellationToken) =>
        store.CloseAsync(id, null, null, true, clock.GetUtcNow().UtcDateTime, cancellationToken);
}
