using Knock.Hosting.Data;
using Knock.Market.Api.Common.Bandeja;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Requests.Data;

/// <summary>Lecturas de un solo campo que los avisos de solicitudes necesitan para saber a quién avisar y de qué recurso.</summary>
internal sealed class DestinatariosDeAvisoStore(
    [FromKeyedServices(RequestsModuleExtensions.ModuleKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> options) : IDestinatariosDeAviso
{
    private const string _technicianUserField = "tecnicoUsuarioId";

    public Task<string?> ClientAsync(string requestId, CancellationToken cancellationToken) =>
        ObjectId.TryParse(requestId, out var request)
            ? ReadAsync("solicitudes", Builders<BsonDocument>.Filter.Eq("_id", request), "clienteId", cancellationToken)
            : Task.FromResult<string?>(null);

    public Task<string?> InvitedTechnicianAsync(string requestId, string technicianId, CancellationToken cancellationToken) =>
        ReadOfRequestAsync("envios", requestId, ("tecnicoId", technicianId), _technicianUserField, cancellationToken);

    public Task<string?> QuotingTechnicianAsync(string requestId, string technicianId, CancellationToken cancellationToken) =>
        ReadOfRequestAsync("cotizaciones", requestId, ("tecnicoId", technicianId), _technicianUserField, cancellationToken);

    public Task<string?> VisitAsync(string requestId, string quoteId, CancellationToken cancellationToken) =>
        ReadOfRequestAsync("visitas", requestId, ("cotizacionId", quoteId), "_id", cancellationToken);

    public async Task<PersonasDeVisita?> VisitPeopleAsync(string visitId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(visitId, out var visit))
        {
            return null;
        }

        var found = await database.GetCollection<BsonDocument>(ChoiceStore.VisitsCollection)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", visit), new FindOptions { MaxTime = options.Value.QueryMaxTime })
            .FirstOrDefaultAsync(cancellationToken);
        if (found is null)
        {
            return null;
        }

        var by = found.TryGetValue("cancelacion", out var cancellation) && cancellation.IsBsonDocument ? DatosDeAviso.Text(cancellation.AsBsonDocument, "por") : null;
        return new PersonasDeVisita(DatosDeAviso.Text(found, _technicianUserField), by);
    }

    private Task<string?> ReadOfRequestAsync(string collection, string requestId, (string Field, string Id) other, string field, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(requestId, out var request) || !ObjectId.TryParse(other.Id, out var second))
        {
            return Task.FromResult<string?>(null);
        }

        var filter = Builders<BsonDocument>.Filter;
        return ReadAsync(collection, filter.Eq("solicitudId", request) & filter.Eq(other.Field, second), field, cancellationToken);
    }

    private async Task<string?> ReadAsync(string collection, FilterDefinition<BsonDocument> filter, string field, CancellationToken cancellationToken)
    {
        var found = await database.GetCollection<BsonDocument>(collection)
            .Find(filter, new FindOptions { MaxTime = options.Value.QueryMaxTime })
            .FirstOrDefaultAsync(cancellationToken);
        return found is not null && found.TryGetValue(field, out var value) && (value.IsString || value.IsObjectId) ? value.ToString() : null;
    }
}
