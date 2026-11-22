using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Identity.Api.Hosting.Procesos;

/// <summary>Filtros y cierres compartidos por <c>outbox</c> y <c>tareas_programadas</c> (spec 054, §6).</summary>
internal static class ReservasMongo
{
    public static FilterDefinition<BsonDocument> Owned(BsonValue id, string reservaId)
    {
        var filter = Builders<BsonDocument>.Filter;
        return filter.And(filter.Eq("_id", id), filter.Eq("estado", "en_proceso"), filter.Eq("reservaId", reservaId));
    }

    public static async Task<bool> CloseAsync(IMongoCollection<BsonDocument> collection, FilterDefinition<BsonDocument> owned, string estado, DateTime now, bool fallido, CancellationToken cancellationToken)
    {
        var result = await collection.UpdateOneAsync(owned, Close(estado, now, fallido), cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    private static UpdateDefinition<BsonDocument> Close(string estado, DateTime now, bool fallido) =>
        Builders<BsonDocument>.Update.Set("estado", estado).Set("actualizadoEn", now)
            .Set("limpiarEn", Reintentos.PoliticaDeReintentos.LimpiarEn(now, fallido))
            .Unset("reservadaHasta").Unset("reservaId");
}
