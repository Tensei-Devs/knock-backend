using Knock.Messaging.Api.Hosting;
using Knock.Messaging.Api.Modules.Conversaciones.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Messaging.Api.Modules.Conversaciones.Data;

/// <summary>Un mensaje guardado. <c>autorId</c> (el <c>sub</c>) existe en la colección pero nunca se lee fuera de ella (R-16).</summary>
internal sealed record StoredMessage(ObjectId Id, string MensajeId, string AutorRol, string Texto, bool ContactoOculto, DateTime CreadoEn);

/// <summary>
/// Colección <c>mensajes</c> (spec 030, §6). Es el único acceso a MongoDB del módulo para los mensajes. Lectura del historial por cursor y conteo de no leídos; la escritura transaccional del
/// envío llega con la tarea T-06.
/// </summary>
internal sealed class MensajesStore(
    [FromKeyedServices(DataExtensions.DatabaseKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> mongoOptions)
{
    public const string MessagesCollection = "mensajes";
    public const string ConversationIndexName = "conversacionId_1_creadoEn_-1__id_-1";
    public const string RetentionIndexName = "creadoEn_1";

    /// <summary>Retención de 180 días desde la creación (R-21), en segundos: la misma cifra que <c>db/030-conversaciones.js</c>.</summary>
    public const int RetentionSeconds = 15_552_000;

    private const string _idField = "_id";
    private const string _conversationField = "conversacionId";
    private const string _createdField = "creadoEn";
    private const string _authorRoleField = "autorRol";

    private readonly FindOptions _findOptions = new() { MaxTime = mongoOptions.Value.QueryMaxTime };

    private IMongoCollection<BsonDocument> Messages => database.GetCollection<BsonDocument>(MessagesCollection);

    /// <summary>Índices de la spec 030 (§6), con los nombres de <c>db/030-conversaciones.js</c>. Idempotente; lo usan las pruebas, y el script de <c>db/</c> hace lo mismo.</summary>
    public Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var keys = Builders<BsonDocument>.IndexKeys;
        return Messages.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<BsonDocument>(keys.Ascending(_conversationField).Descending(_createdField).Descending(_idField), new CreateIndexOptions { Name = ConversationIndexName }),
                new CreateIndexModel<BsonDocument>(keys.Ascending(_createdField), new CreateIndexOptions { Name = RetentionIndexName, ExpireAfter = TimeSpan.FromSeconds(RetentionSeconds) }),
            ],
            cancellationToken);
    }

    /// <summary>Los mensajes de la conversación del más reciente al más antiguo, empezando después del cursor. Solo trae lo que sale en la respuesta (sin <c>autorId</c>).</summary>
    public async Task<IReadOnlyList<StoredMessage>> ListAsync(ObjectId conversationId, PageCursor? after, int take, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var filter = filters.Eq(_conversationField, conversationId);
        if (after is { } cursor)
        {
            filter &= filters.Lt(_createdField, cursor.Date) | (filters.Eq(_createdField, cursor.Date) & filters.Lt(_idField, cursor.Id));
        }

        var documents = await Messages.Find(filter, _findOptions)
            .Project(Builders<BsonDocument>.Projection.Exclude("autorId"))
            .Sort(Builders<BsonDocument>.Sort.Descending(_createdField).Descending(_idField))
            .Limit(take)
            .ToListAsync(cancellationToken);
        return [.. documents.Select(d => new StoredMessage(
            d[_idField].AsObjectId,
            d["mensajeId"].AsString,
            d[_authorRoleField].AsString,
            d["texto"].AsString,
            d["contactoOculto"].AsBoolean,
            d[_createdField].ToUniversalTime()))];
    }

    /// <summary>Cuántos mensajes de la otra parte (no escritos por <paramref name="role"/>) son posteriores a <paramref name="readUntil"/> (R-19).</summary>
    public async Task<int> CountUnreadAsync(ObjectId conversationId, string role, DateTime? readUntil, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var filter = filters.Eq(_conversationField, conversationId) & filters.Ne(_authorRoleField, role);
        if (readUntil is { } mark)
        {
            filter &= filters.Gt(_createdField, mark);
        }

        return checked((int)await Messages.CountDocumentsAsync(filter, new CountOptions { MaxTime = mongoOptions.Value.QueryMaxTime }, cancellationToken));
    }
}
