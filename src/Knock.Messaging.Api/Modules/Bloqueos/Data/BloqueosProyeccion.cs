using Knock.Messaging.Api.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Messaging.Api.Modules.Bloqueos.Data;

/// <summary>Proyección mínima <c>bloqueos</c> de Mensajería (spec 043, R-23): solo los dos identificadores públicos, el estado y la fecha del último evento. El <c>_id</c> es el par («bloquea|bloqueado»), así que no necesita más índices. Nunca lee la base de Identidad.</summary>
internal interface IBloqueosProyeccion : IBloqueosApi
{
    /// <summary>Aplica <c>BloqueoCreado</c> (<paramref name="blocked"/> verdadero) o <c>BloqueoEliminado</c>. Idempotente y a prueba de desorden: un evento más viejo que el ya aplicado no cambia nada.</summary>
    Task ApplyAsync(string blockerId, string blockedId, bool blocked, DateTime occurredAtUtc, string eventId, CancellationToken cancellationToken);
}

internal sealed class BloqueosProyeccion([FromKeyedServices(DataExtensions.DatabaseKey)] IMongoDatabase database) : IBloqueosProyeccion
{
    public const string CollectionName = "bloqueos";
    private const string _blockedField = "bloqueado";
    private const string _atField = "ocurridoEn";

    private IMongoCollection<BsonDocument> Blocks => database.GetCollection<BsonDocument>(CollectionName);

    public async Task ApplyAsync(string blockerId, string blockedId, bool blocked, DateTime occurredAtUtc, string eventId, CancellationToken cancellationToken)
    {
        var key = PairKey(blockerId, blockedId);
        var at = new BsonDateTime(occurredAtUtc);
        var current = await Blocks.Find(Builders<BsonDocument>.Filter.Eq("_id", key)).Project(Builders<BsonDocument>.Projection.Include(_atField)).FirstOrDefaultAsync(cancellationToken);
        if (current is not null && current[_atField].ToUniversalTime() > occurredAtUtc)
        {
            return;
        }

        var update = Builders<BsonDocument>.Update.Set("bloqueaId", blockerId).Set("bloqueadoId", blockedId).Set(_blockedField, blocked).Set(_atField, at).Set("eventoId", eventId);
        try
        {
            await Blocks.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", key), update, new UpdateOptions { IsUpsert = true }, cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Dos entregas del mismo par a la vez: la otra ganó la inserción y ambas dejan el mismo estado final.
        }
    }

    public async Task<bool> AreBlockedAsync(string personA, string personB, CancellationToken cancellationToken)
    {
        var count = await Blocks.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.In("_id", [PairKey(personA, personB), PairKey(personB, personA)]),
                Builders<BsonDocument>.Filter.Eq(_blockedField, true)),
            new CountOptions { Limit = 1 },
            cancellationToken);
        return count > 0;
    }

    private static string PairKey(string blocker, string blocked) => $"{blocker}|{blocked}";
}
