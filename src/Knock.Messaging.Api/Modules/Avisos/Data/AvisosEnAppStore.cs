using Knock.Hosting.Concurrency;
using Knock.Messaging.Api.Modules.Avisos.Domain;
using Knock.Messaging.Api.Modules.Avisos.Push;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Messaging.Api.Modules.Avisos.Data;

/// <summary>
/// Escribe la colección <c>avisos</c> (spec 044 §6) por mandato de la spec 037 R-14: documento con texto fijo del catálogo, <c>destino</c> sin enlaces, <c>leido</c> falso y vencimiento a 30 días.
/// Idempotente por <c>eventoId</c> y <c>usuarioId</c> (índice único parcial de <c>db/044-avisos-dentro-de-la-app.js</c>): la entrega repetida no duplica; las escrituras de la misma clave se
/// turnan con <see cref="KeyedGate"/> para no chocar en el índice único (interbloqueo de documentdb-local). La lectura vive en <c>NoticesStore</c> (módulo Notices).
/// </summary>
internal sealed class AvisosEnAppStore(IMongoDatabase database) : IAvisosEnApp
{
    public const string CollectionName = "avisos";

    private readonly IMongoCollection<BsonDocument> _notices = database.GetCollection<BsonDocument>(CollectionName);

    public Task<bool> CreateAsync(AvisoEnApp aviso, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(aviso);
        return KeyedGate.RunAsync("avisos|" + AvisoRules.DeduplicationKey(aviso.EventoId, aviso.Destinatario), () => InsertAsync(aviso, cancellationToken), cancellationToken);
    }

    private async Task<bool> InsertAsync(AvisoEnApp aviso, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        if (await _notices.Find(filters.Eq("eventoId", aviso.EventoId) & filters.Eq("usuarioId", aviso.Destinatario)).Limit(1).AnyAsync(cancellationToken))
        {
            return false;
        }

        var created = aviso.CreadoEn;
        var document = new BsonDocument
        {
            ["usuarioId"] = aviso.Destinatario,
            ["eventoId"] = aviso.EventoId,
            ["comoRol"] = aviso.Rol is null ? BsonNull.Value : aviso.Rol,
            ["tipo"] = aviso.Tipo,
            ["titulo"] = aviso.Plantilla.Title,
            ["texto"] = aviso.Plantilla.Text,
            ["destino"] = new BsonDocument { ["pantalla"] = aviso.Plantilla.Screen, ["id"] = aviso.Recurso },
            ["leido"] = false,
            ["creadoEn"] = new BsonDateTime(created),
            ["venceEn"] = new BsonDateTime(created.AddDays(AvisoCatalog.RetentionDays)),
        };
        try
        {
            await _notices.InsertOneAsync(document, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }
}
