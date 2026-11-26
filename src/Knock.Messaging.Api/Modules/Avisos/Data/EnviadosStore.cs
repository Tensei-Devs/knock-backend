using Knock.Messaging.Api.Modules.Avisos.Domain;
using Knock.Messaging.Api.Modules.Avisos.Push;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Messaging.Api.Modules.Avisos.Data;

/// <summary>
/// <c>notifications.enviados</c> (deduplicación, límite por persona y resultado, sin texto ni recurso en claro, spec 037 R-06) y <c>notifications.pendientes</c> (ventana de 5 minutos de
/// <c>mensaje_nuevo</c>, R-07). Los índices los crea <c>db/037-notificaciones.js</c>.
/// </summary>
internal sealed class EnviadosStore(IMongoDatabase database) : IEnviadosStore
{
    private readonly IMongoCollection<BsonDocument> _sent = database.GetCollection<BsonDocument>("notifications.enviados");
    private readonly IMongoCollection<BsonDocument> _windows = database.GetCollection<BsonDocument>("notifications.pendientes");

    public async Task<bool> TryReserveAsync(string deduplicationKey, string evento, string tipo, CancellationToken cancellationToken)
    {
        var (eventId, person) = AvisoRules.SplitDeduplicationKey(deduplicationKey);
        var document = new BsonDocument
        {
            ["eventoId"] = eventId,
            ["destinatario"] = person,
            ["evento"] = evento,
            ["tipo"] = tipo,
            ["resultado"] = "pendiente",
            ["creadoEn"] = new BsonDateTime(DateTime.UtcNow),
        };
        try
        {
            await _sent.InsertOneAsync(document, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public Task RecordResultAsync(string deduplicationKey, string result, CancellationToken cancellationToken)
    {
        var (eventId, person) = AvisoRules.SplitDeduplicationKey(deduplicationKey);
        var filters = Builders<BsonDocument>.Filter;
        return _sent.UpdateOneAsync(
            filters.Eq("eventoId", eventId) & filters.Eq("destinatario", person),
            Builders<BsonDocument>.Update.Set("resultado", result),
            cancellationToken: cancellationToken);
    }

    public async Task<DateTime?> GetWindowStartAsync(string conversation, string person, CancellationToken cancellationToken)
    {
        var window = await _windows.Find(WindowFilter(conversation, person)).FirstOrDefaultAsync(cancellationToken);
        return window is null ? null : window["creadoEn"].ToUniversalTime();
    }

    public Task OpenWindowAsync(string conversation, string person, DateTime nowUtc, CancellationToken cancellationToken) =>
        _windows.UpdateOneAsync(
            WindowFilter(conversation, person),
            Builders<BsonDocument>.Update
                .Set("creadoEn", new BsonDateTime(nowUtc))
                .Set("cierraEn", new BsonDateTime(nowUtc.Add(AvisoRules.MessageWindow)))
                .Set("suprimidos", 0),
            new UpdateOptions { IsUpsert = true },
            cancellationToken);

    public Task CountSuppressedAsync(string conversation, string person, CancellationToken cancellationToken) =>
        _windows.UpdateOneAsync(WindowFilter(conversation, person), Builders<BsonDocument>.Update.Inc("suprimidos", 1), cancellationToken: cancellationToken);

    public async Task<int> CountRecentAsync(string person, DateTime sinceUtc, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var count = await _sent.CountDocumentsAsync(filters.Eq("destinatario", person) & filters.Gte("creadoEn", new BsonDateTime(sinceUtc)), cancellationToken: cancellationToken);
        return (int)Math.Min(count, int.MaxValue);
    }

    private static FilterDefinition<BsonDocument> WindowFilter(string conversation, string person)
    {
        var filters = Builders<BsonDocument>.Filter;
        return filters.Eq("conversacionId", conversation) & filters.Eq("destinatario", person);
    }
}
