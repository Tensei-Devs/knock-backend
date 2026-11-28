using Knock.Market.Api.Modules.Requests.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Requests.Data;

/// <summary>
/// La solicitud sigue a su visita (specs 020 y 029, enmienda 2026-11-26). Se llama dentro de la misma transacción que cierra la visita. Solo cambia una solicitud en <c>cita_confirmada</c>:
/// en cualquier otro estado no hace nada. La cotización aceptada no cambia y la solicitud cancelada no se reabre.
/// </summary>
internal static class SolicitudDeLaVisita
{
    public static Task FinishAsync(IMongoDatabase database, IClientSessionHandle transaction, ObjectId requestId, DateTime nowUtc, CancellationToken cancellationToken) =>
        CloseAsync(database, transaction, requestId, Update(RequestState.Terminada, nowUtc).Set("terminadaEn", nowUtc), cancellationToken);

    public static Task CancelAsync(IMongoDatabase database, IClientSessionHandle transaction, ObjectId requestId, string reason, DateTime nowUtc, CancellationToken cancellationToken) =>
        CloseAsync(database, transaction, requestId, Update(RequestState.Cancelada, nowUtc).Set("canceladaEn", nowUtc).Set("motivoCancelacion", reason), cancellationToken);

    private static UpdateDefinition<BsonDocument> Update(RequestState state, DateTime nowUtc) =>
        Builders<BsonDocument>.Update.Set("estado", RequestStates.ToStored(state)).Set("actualizadoEn", nowUtc);

    private static Task<UpdateResult> CloseAsync(IMongoDatabase database, IClientSessionHandle transaction, ObjectId requestId, UpdateDefinition<BsonDocument> update, CancellationToken cancellationToken) =>
        database.GetCollection<BsonDocument>("solicitudes").UpdateOneAsync(
            transaction,
            Builders<BsonDocument>.Filter.Eq("_id", requestId) & Builders<BsonDocument>.Filter.Eq("estado", RequestStates.ToStored(RequestState.CitaConfirmada)),
            update,
            cancellationToken: cancellationToken);
}
