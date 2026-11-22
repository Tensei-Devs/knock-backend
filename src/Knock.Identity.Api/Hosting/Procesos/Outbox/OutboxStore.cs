using Knock.Identity.Api.Hosting.Procesos.Reintentos;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Identity.Api.Hosting.Procesos.Outbox;

/// <summary>Colección técnica <c>outbox</c> de Identidad (spec 054, §6).</summary>
internal sealed class OutboxStore(IMongoDatabase database) : IOutboxStore
{
    private readonly IMongoCollection<BsonDocument> _events = database.GetCollection<BsonDocument>("outbox");

    public async Task<bool> AddAsync(IClientSessionHandle session, EventoDeOutbox evento, DateTime now, CancellationToken cancellationToken)
    {
        OutboxReglas.Validate(evento);
        var update = Builders<BsonDocument>.Update.SetOnInsert("tipo", evento.Tipo).SetOnInsert("destino", evento.Destino).SetOnInsert("carga", evento.Carga)
            .SetOnInsert("estado", "pendiente").SetOnInsert("programadaPara", now).SetOnInsert("intentos", 0).SetOnInsert("creadoEn", now).SetOnInsert("actualizadoEn", now);
        var result = await _events.UpdateOneAsync(session, Builders<BsonDocument>.Filter.Eq("_id", evento.Id), update, new UpdateOptions { IsUpsert = true }, cancellationToken);
        return result.UpsertedId is not null;
    }

    public async Task<OutboxReservado?> ReserveAsync(DateTime now, CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter;
        var due = (filter.Eq("estado", "pendiente") & filter.Lte("programadaPara", now))
            | (filter.Eq("estado", "en_proceso") & filter.Lt("reservadaHasta", now));
        var token = Guid.NewGuid().ToString("N");
        var update = Builders<BsonDocument>.Update.Set("estado", "en_proceso")
            .Set("reservadaHasta", now.AddMinutes(PoliticaDeReintentos.ArrendamientoMinutos)).Set("reservaId", token).Set("actualizadoEn", now);
        var document = await _events.FindOneAndUpdateAsync(due, update,
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After, Sort = Builders<BsonDocument>.Sort.Ascending("programadaPara") },
            cancellationToken);
        return document is null
            ? null
            : new OutboxReservado(document["_id"].AsString, document["tipo"].AsString, document["destino"].AsString, document["carga"].AsBsonDocument, token, document.GetValue("intentos", 0).ToInt32());
    }

    public Task<bool> MarkDeliveredAsync(OutboxReservado evento, DateTime now, CancellationToken cancellationToken)
    =>
        ReservasMongo.CloseAsync(_events, Owned(evento), "entregado", now, fallido: false, cancellationToken);

    public async Task<bool> FailAsync(OutboxReservado evento, bool permanent, DateTime now, CancellationToken cancellationToken)
    {
        var attempts = evento.Intentos + 1;
        var delay = permanent ? null : PoliticaDeReintentos.Espera(attempts);
        var update = Builders<BsonDocument>.Update.Set("estado", delay is null ? "fallido" : "pendiente")
            .Set("intentos", attempts).Set("actualizadoEn", now).Unset("reservadaHasta").Unset("reservaId");
        update = delay is { } wait
            ? update.Set("programadaPara", now + wait)
            : update.Set("limpiarEn", PoliticaDeReintentos.LimpiarEn(now, fallido: true));
        return (await _events.UpdateOneAsync(Owned(evento), update, cancellationToken: cancellationToken)).ModifiedCount == 1;
    }

    private static FilterDefinition<BsonDocument> Owned(OutboxReservado evento) => ReservasMongo.Owned(evento.Id, evento.ReservaId);
}
