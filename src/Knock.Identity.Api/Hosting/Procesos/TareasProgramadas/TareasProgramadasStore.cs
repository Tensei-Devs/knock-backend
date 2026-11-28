using Knock.Identity.Api.Hosting.Procesos.Reintentos;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;

/// <summary>Colección técnica <c>tareas_programadas</c> de Identidad (spec 054, §6): reserva atómica con arrendamiento y limpieza por <c>limpiarEn</c>.</summary>
internal sealed class TareasProgramadasStore(IMongoDatabase database) : ITareasProgramadasStore
{
    private readonly IMongoCollection<BsonDocument> _tasks = database.GetCollection<BsonDocument>("tareas_programadas");

    public async Task<bool> ScheduleAsync(IClientSessionHandle? session, string type, string businessKey, DateTime due, DateTime now, CancellationToken cancellationToken)
    {
        if (!CatalogoDeTareas.Contiene(type))
        {
            throw new ArgumentException("unknown_task_type", nameof(type));
        }

        var filter = Builders<BsonDocument>.Filter.Eq("tipo", type) & Builders<BsonDocument>.Filter.Eq("claveNegocio", businessKey);
        var update = Builders<BsonDocument>.Update.SetOnInsert("programadaPara", due).SetOnInsert("estado", "pendiente")
            .SetOnInsert("intentos", 0).SetOnInsert("creadoEn", now).SetOnInsert("actualizadoEn", now);
        var options = new UpdateOptions { IsUpsert = true };
        var result = session is null
            ? await _tasks.UpdateOneAsync(filter, update, options, cancellationToken)
            : await _tasks.UpdateOneAsync(session, filter, update, options, cancellationToken);
        return result.UpsertedId is not null;
    }

    public async Task<TareaReservada?> ReserveAsync(DateTime now, IReadOnlyCollection<string> excludedTypes, CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter;
        var due = (filter.Eq("estado", "pendiente") & filter.Lte("programadaPara", now))
            | (filter.Eq("estado", "en_proceso") & filter.Lt("reservadaHasta", now));
        if (excludedTypes.Count > 0)
        {
            due &= filter.Nin("tipo", excludedTypes);
        }

        var token = Guid.NewGuid().ToString("N");
        var update = Builders<BsonDocument>.Update.Set("estado", "en_proceso")
            .Set("reservadaHasta", now.AddMinutes(PoliticaDeReintentos.ArrendamientoMinutos)).Set("reservaId", token).Set("actualizadoEn", now);
        var task = await _tasks.FindOneAndUpdateAsync(due, update,
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After, Sort = Builders<BsonDocument>.Sort.Ascending("programadaPara") },
            cancellationToken);
        return task is null ? null : new TareaReservada(task["_id"].AsObjectId, task["tipo"].AsString, task["claveNegocio"].AsString, token, task.GetValue("intentos", 0).ToInt32());
    }

    public Task<bool> CompleteAsync(TareaReservada task, DateTime now, CancellationToken cancellationToken)
    =>
        ReservasMongo.CloseAsync(_tasks, Owned(task), "hecha", now, fallido: false, cancellationToken);

    public async Task<bool> FailAsync(TareaReservada task, DateTime now, CancellationToken cancellationToken)
    {
        var attempts = task.Intentos + 1;
        var delay = PoliticaDeReintentos.Espera(attempts);
        var update = Builders<BsonDocument>.Update.Set("estado", delay is null ? "fallida" : "pendiente")
            .Set("intentos", attempts).Set("actualizadoEn", now).Unset("reservadaHasta").Unset("reservaId");
        update = delay is { } wait
            ? update.Set("programadaPara", now + wait)
            : update.Set("limpiarEn", PoliticaDeReintentos.LimpiarEn(now, fallido: true));
        return (await _tasks.UpdateOneAsync(Owned(task), update, cancellationToken: cancellationToken)).ModifiedCount == 1;
    }

    public Task<bool> AbortAsync(TareaReservada task, DateTime now, CancellationToken cancellationToken)
    =>
        ReservasMongo.CloseAsync(_tasks, Owned(task), "fallida", now, fallido: true, cancellationToken);

    // Debe llamarse en la misma transacción que las escrituras del manejador: una escritura (no una lectura) fuerza el conflicto si otra réplica retomó el arrendamiento.
    public async Task<bool> ConfirmLeaseAsync(IClientSessionHandle session, TareaReservada task, DateTime now, CancellationToken cancellationToken)
    {
        var stillOwned = Builders<BsonDocument>.Filter.And(Owned(task), Builders<BsonDocument>.Filter.Gt("reservadaHasta", now));
        var touched = await _tasks.UpdateOneAsync(session, stillOwned, Builders<BsonDocument>.Update.Inc("confirmacionesReserva", 1), cancellationToken: cancellationToken);
        return touched.MatchedCount == 1;
    }

    private static FilterDefinition<BsonDocument> Owned(TareaReservada task) => ReservasMongo.Owned(task.Id, task.ReservaId);
}
