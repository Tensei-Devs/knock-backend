using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Tecnicos.Data;

/// <summary>Escritura de la ocultación por moderación en <c>tecnicos</c> (spec 053, R-20).</summary>
internal interface ITechnicianModerationStore
{
    /// <summary>Oculta al técnico por la decisión de un reporte. Falso si ya estaba oculto por ese mismo reporte o si no existe (reintentar no cambia nada).</summary>
    Task<bool> HideAsync(string usuarioId, string reporteId, DateTime nowUtc, CancellationToken cancellationToken);
}

/// <summary>
/// Pone <c>oculto</c> en verdadero y deja el reporte que lo causó en <c>ocultoPorReporteId</c>. El filtro excluye al técnico ya oculto por ese reporte, así que el evento entregado dos veces no
/// reescribe nada. Nunca toca <c>ocultoHasta</c>: la ocultación por moderación no caduca sola.
/// </summary>
internal sealed class TechnicianModerationStore(
    [FromKeyedServices(TecnicosModuleExtensions.ModuleKey)] IMongoDatabase database) : ITechnicianModerationStore
{
    public async Task<bool> HideAsync(string usuarioId, string reporteId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var filter = filters.Eq("usuarioId", usuarioId) & filters.Ne("ocultoPorReporteId", reporteId);
        var update = Builders<BsonDocument>.Update.Set("oculto", true).Set("ocultoPorReporteId", reporteId).Set("ocultoEn", nowUtc);
        var result = await database.GetCollection<BsonDocument>(TechnicianProfileStore.CollectionName)
            .UpdateOneAsync(filter, update, null, cancellationToken);
        return result.ModifiedCount > 0;
    }
}
