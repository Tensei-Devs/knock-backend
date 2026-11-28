using Knock.Identity.Api.Hosting.Procesos.Outbox;
using Knock.Identity.Api.Modules.Account.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Identity.Api.Modules.Account.Data;

/// <summary>
/// Aviso <c>exportacion_lista</c> (spec 039, R-22; spec 037) por la bandeja de salida (spec 054): Identidad lo entrega a <c>POST /api/v1/interno/avisos</c> de Mensajería con texto fijo del catálogo y sin enlace.
/// El <c>_id</c> es determinista por exportación, así que reintentar la tarea no duplica el aviso. La carga lleva solo la persona (<c>usuarioId</c>, su <c>sub</c>) y el recurso (su identificador interno).
/// </summary>
internal sealed class OutboxExportNotice(IOutboxStore outbox, IMongoClient client, TimeProvider clock) : IExportNotice
{
    public const string EventType = "exportacion_lista";

    public async Task NotifyReadyAsync(string userId, string sub, string exportKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportKey);
        var eventId = OutboxReglas.BuildId(EventType, exportKey, 1);
        var evento = new EventoDeOutbox(eventId, EventType, "messaging", new BsonDocument { ["eventoId"] = eventId, ["usuarioId"] = sub, ["recurso"] = userId });
        using var session = await client.StartSessionAsync(cancellationToken: cancellationToken);
        await outbox.AddAsync(session, evento, clock.GetUtcNow().UtcDateTime, cancellationToken);
    }
}
