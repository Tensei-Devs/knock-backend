using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Requests.Data;

/// <summary>Lectura mínima de participación (spec 052): el técnico participa si la solicitud le llegó y sigue visible para él; el cierre sale de la propia solicitud. No proyecta dirección ni contacto.</summary>
internal sealed class TechnicianAccessStore([FromKeyedServices(RequestsModuleExtensions.ModuleKey)] IMongoDatabase database)
{
    private static readonly string[] _visibleStates = ["activo", "cotizado"];
    private static readonly string[] _closedFields = ["terminadaEn", "canceladaEn", "vencidaEn"];
    private static readonly string[] _closedStates = ["terminada", "calificada", "cancelada", "vencida"];
    private static readonly ProjectionDefinition<BsonDocument> _closingProjection =
        Builders<BsonDocument>.Projection.Include("estado").Include("actualizadoEn").Include("terminadaEn").Include("canceladaEn").Include("vencidaEn");

    public async Task<(bool Participates, DateTime? ClosedAtUtc)> GetAsync(string solicitudId, string sub, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(solicitudId, out var id) || string.IsNullOrEmpty(sub))
        {
            return (false, null);
        }

        var f = Builders<BsonDocument>.Filter;
        var sent = await database.GetCollection<BsonDocument>("envios")
            .Find(f.Eq("solicitudId", id) & f.Eq("tecnicoUsuarioId", sub) & f.In("estado", _visibleStates))
            .Project(Builders<BsonDocument>.Projection.Include("_id")).FirstOrDefaultAsync(cancellationToken);
        if (sent is null)
        {
            return (false, null);
        }

        var request = await database.GetCollection<BsonDocument>("solicitudes").Find(f.Eq("_id", id))
            .Project(_closingProjection)
            .FirstOrDefaultAsync(cancellationToken);
        return (true, request is null ? null : ClosedAt(request));
    }

    public async Task<IReadOnlyDictionary<string, DateTime>> GetClosedAtAsync(IReadOnlyCollection<string> solicitudIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitudIds);
        var ids = new List<ObjectId>(solicitudIds.Count);
        foreach (var solicitudId in solicitudIds)
        {
            if (ObjectId.TryParse(solicitudId, out var parsed))
            {
                ids.Add(parsed);
            }
        }

        var found = await database.GetCollection<BsonDocument>("solicitudes").Find(Builders<BsonDocument>.Filter.In("_id", ids))
            .Project(_closingProjection)
            .ToListAsync(cancellationToken);
        var closed = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        foreach (var request in found)
        {
            if (ClosedAt(request) is { } at)
            {
                closed[request["_id"].AsObjectId.ToString()] = at;
            }
        }

        return closed;
    }

    private static DateTime? ClosedAt(BsonDocument request)
    {
        if (!request.TryGetValue("estado", out var state) || !state.IsString || !_closedStates.Contains(state.AsString))
        {
            return null;
        }

        foreach (var field in _closedFields)
        {
            if (request.TryGetValue(field, out var value) && value.IsValidDateTime)
            {
                return value.ToUniversalTime();
            }
        }

        return request.TryGetValue("actualizadoEn", out var updated) && updated.IsValidDateTime ? updated.ToUniversalTime() : null;
    }
}
