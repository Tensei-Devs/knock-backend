using Knock.Market.Api.Common.Bandeja;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Requests.Data;

/// <summary>Reserva, cierre y reintento de los mensajes de la colección <c>outbox</c> de Mercado (la que escribe <see cref="MongoOutbox"/>), con arrendamiento de 2 minutos como las tareas programadas.</summary>
internal sealed class BandejaStore(IMongoDatabase database) : IBandejaStore
{
    private readonly IMongoCollection<BsonDocument> _messages = database.GetCollection<BsonDocument>(MongoOutbox.CollectionName);

    public async Task<MensajeReservado?> ReserveAsync(DateTime now, IReadOnlyCollection<string> types, CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter;
        var due = filter.In("tipo", types)
            & ((filter.Eq("estado", "pendiente") & (filter.Exists("programadaPara", false) | filter.Lte("programadaPara", now))) | (filter.Eq("estado", "en_proceso") & filter.Lt("reservadaHasta", now)));
        var token = Guid.NewGuid().ToString("N");
        var update = Builders<BsonDocument>.Update.Set("estado", "en_proceso").Set("reservadaHasta", now.AddMinutes(2)).Set("reservaId", token).Set("actualizadoEn", now);
        var found = await _messages.FindOneAndUpdateAsync(due, update, new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After, Sort = Builders<BsonDocument>.Sort.Ascending("creadoEn") }, cancellationToken);
        return found is null ? null : new MensajeReservado(found["_id"].AsString, found["tipo"].AsString, found["datos"].AsBsonDocument, token, found.GetValue("intentos", 0).ToInt32());
    }

    public async Task<bool> MarkDeliveredAsync(MensajeReservado mensaje, DateTime now, CancellationToken cancellationToken) =>
        (await _messages.UpdateOneAsync(Owned(mensaje), Builders<BsonDocument>.Update.Set("estado", "entregado").Set("entregadoEn", now).Set("actualizadoEn", now).Unset("reservadaHasta").Unset("reservaId"), cancellationToken: cancellationToken)).ModifiedCount == 1;

    public async Task<bool> FailAsync(MensajeReservado mensaje, bool permanent, DateTime now, CancellationToken cancellationToken)
    {
        var attempts = mensaje.Intentos + 1;
        var delay = permanent ? null : DespachadorDeBandeja.Espera(attempts);
        var update = Builders<BsonDocument>.Update.Set("estado", delay is null ? "fallido" : "pendiente").Set("intentos", attempts).Set("actualizadoEn", now).Unset("reservadaHasta").Unset("reservaId");
        if (delay is { } wait)
        {
            update = update.Set("programadaPara", now + wait);
        }

        return (await _messages.UpdateOneAsync(Owned(mensaje), update, cancellationToken: cancellationToken)).ModifiedCount == 1;
    }

    private static FilterDefinition<BsonDocument> Owned(MensajeReservado mensaje) =>
        Builders<BsonDocument>.Filter.Eq("_id", mensaje.Id) & Builders<BsonDocument>.Filter.Eq("reservaId", mensaje.ReservaId);
}
