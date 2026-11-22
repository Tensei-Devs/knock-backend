using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Requests.Data;

/// <summary>
/// Acceso a las solicitudes, cotizaciones y visitas de una persona para la cuenta (spec 039): pendientes (R-24), cancelación al programar la baja (R-25), exportación (R-22) y anonimizado al ejecutarla.
/// Las lecturas y escrituras siempre se filtran por el identificador de la persona del token de servicio; nada de otras personas sale de aquí.
/// </summary>
internal sealed class AccountDataStore(
    [FromKeyedServices(RequestsModuleExtensions.ModuleKey)] IMongoDatabase database,
    IOutbox outbox)
{
    public const int MaxIds = 50;
    public const string AccountDeletedReason = "account_deleted";
    private const string _quotesCollection = "cotizaciones";
    private const string _stateField = "estado";
    private const string _technicianField = "tecnicoUsuarioId";
    private const string _updatedField = "actualizadoEn";
    private const string _sentState = "enviada";
    private static readonly string[] _openRequestStates = ["enviada", "cotizando"];
    private static readonly string[] _requestFields = ["_id", "oficioNombre", "modalidad", "descripcion", "dia", "franja", "coloniaNombre", "estado", "creadoEn"];
    private static readonly string[] _quoteFields = ["_id", "solicitudId", "tipoPrecio", "precio", "precioMin", "precioMax", "dia", "hora", "nota", "estado", "creadoEn"];
    private static readonly string[] _visitFields = ["_id", "solicitudId", "modalidad", "dia", "hora", "precio", "estado", "creadoEn"];

    private IMongoCollection<BsonDocument> Requests => database.GetCollection<BsonDocument>("solicitudes");

    private IMongoCollection<BsonDocument> Quotes => database.GetCollection<BsonDocument>(_quotesCollection);

    private IMongoCollection<BsonDocument> Visits => database.GetCollection<BsonDocument>("visitas");

    private static FilterDefinition<BsonDocument> OfPerson(string sub) =>
        Builders<BsonDocument>.Filter.Or(Builders<BsonDocument>.Filter.Eq("clienteId", sub), Builders<BsonDocument>.Filter.Eq(_technicianField, sub));

    public async Task<AccountPendingResponse> PendingAsync(string sub, CancellationToken cancellationToken)
    {
        var f = Builders<BsonDocument>.Filter;
        var appointments = f.And(OfPerson(sub), f.Eq(_stateField, VisitRules.Confirmed));
        var quotes = f.And(f.Eq(_technicianField, sub), f.Eq(_stateField, _sentState));
        var inProgress = f.And(OfPerson(sub), f.Eq(_stateField, VisitRules.InProgress));
        return new AccountPendingResponse(
            (int)await Visits.CountDocumentsAsync(appointments, cancellationToken: cancellationToken),
            await IdsAsync(Visits, appointments, cancellationToken),
            (int)await Quotes.CountDocumentsAsync(quotes, cancellationToken: cancellationToken),
            await IdsAsync(Quotes, quotes, cancellationToken),
            await Visits.Find(inProgress).Limit(1).AnyAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<ObjectId>> OpenRequestIdsAsync(string sub, CancellationToken cancellationToken)
    {
        var f = Builders<BsonDocument>.Filter;
        var found = await Requests.Find(f.Eq("clienteId", sub) & f.In(_stateField, _openRequestStates)).Project(Builders<BsonDocument>.Projection.Include("_id")).ToListAsync(cancellationToken);
        return [.. found.Select(x => x["_id"].AsObjectId)];
    }

    public async Task<IReadOnlyList<VisitRecord>> ConfirmedVisitsAsync(string sub, CancellationToken cancellationToken)
    {
        var f = Builders<BsonDocument>.Filter;
        var found = await Visits.Find(f.And(OfPerson(sub), f.Eq(_stateField, VisitRules.Confirmed))).ToListAsync(cancellationToken);
        return [.. found.Select(VisitStore.ToRecord)];
    }

    /// <summary>Retira las cotizaciones <c>enviada</c> de la persona y deja <c>CotizacionRetirada</c> (solo identificadores) para avisar al cliente. Devuelve cuántas retiró.</summary>
    public async Task<int> WithdrawQuotesAsync(string sub, DateTime now, CancellationToken cancellationToken)
    {
        var f = Builders<BsonDocument>.Filter;
        var pending = await Quotes.Find(f.Eq(_technicianField, sub) & f.Eq(_stateField, _sentState)).Project(Builders<BsonDocument>.Projection.Include("solicitudId")).ToListAsync(cancellationToken);
        var withdrawn = 0;
        foreach (var quote in pending)
        {
            var id = quote["_id"].AsObjectId;
            var done = await MarketTransactions.RunAsync(database, async (transaction, token) =>
            {
                var changed = await Quotes.UpdateOneAsync(
                    transaction,
                    f.Eq("_id", id) & f.Eq(_stateField, _sentState),
                    Builders<BsonDocument>.Update.Set(_stateField, "retirada").Set("motivoRetiro", AccountDeletedReason).Set(_updatedField, now),
                    cancellationToken: token);
                if (changed.MatchedCount != 1)
                {
                    return false;
                }

                var payload = new BsonDocument { { "cotizacionId", id.ToString() }, { "solicitudId", quote["solicitudId"].ToString() } };
                await outbox.AddAsync(transaction, OutboxMessage.Create("CotizacionRetirada", id.ToString(), payload, now), token);
                return true;
            }, false, cancellationToken);
            withdrawn += done ? 1 : 0;
        }

        return withdrawn;
    }

    /// <summary>Datos de la persona en Mercado para la exportación (R-22): solo campos propios, sin direcciones, códigos de visita ni fotos.</summary>
    public async Task<BsonDocument> ExportAsync(string sub, CancellationToken cancellationToken)
    {
        var f = Builders<BsonDocument>.Filter;
        var requests = await Requests.Find(f.Eq("clienteId", sub)).Limit(1000).ToListAsync(cancellationToken);
        var quotes = await Quotes.Find(f.Eq(_technicianField, sub)).Limit(1000).ToListAsync(cancellationToken);
        var visits = await Visits.Find(OfPerson(sub)).Limit(1000).ToListAsync(cancellationToken);
        return new BsonDocument
        {
            { "solicitudes", new BsonArray(requests.Select(x => Pick(x, _requestFields))) },
            { "cotizaciones", new BsonArray(quotes.Select(x => Pick(x, _quoteFields))) },
            { "visitas", new BsonArray(visits.Select(x => Pick(x, _visitFields))) },
        };
    }

    /// <summary>
    /// Borra de Mercado lo personal de la persona que ya no existe (R-25): descripciones, direcciones y fotos de sus solicitudes y notas de sus cotizaciones. Conserva los identificadores y los estados para que las otras partes no pierdan su historial. Repetirlo no cambia nada.
    /// </summary>
    public async Task AnonymizeAsync(string sub, DateTime now, CancellationToken cancellationToken)
    {
        var f = Builders<BsonDocument>.Filter;
        await Requests.UpdateManyAsync(
            f.Eq("clienteId", sub),
            Builders<BsonDocument>.Update.Set("descripcion", string.Empty).Set("fotos", new BsonArray()).Unset("direccionCifrada").Set(_updatedField, now),
            cancellationToken: cancellationToken);
        await Quotes.UpdateManyAsync(f.Eq(_technicianField, sub), Builders<BsonDocument>.Update.Unset("nota").Set(_updatedField, now), cancellationToken: cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> IdsAsync(IMongoCollection<BsonDocument> collection, FilterDefinition<BsonDocument> filter, CancellationToken cancellationToken)
    {
        var found = await collection.Find(filter).Sort(Builders<BsonDocument>.Sort.Descending("_id")).Limit(MaxIds).Project(Builders<BsonDocument>.Projection.Include("_id")).ToListAsync(cancellationToken);
        return [.. found.Select(x => x["_id"].ToString()!)];
    }

    private static BsonDocument Pick(BsonDocument source, string[] fields)
    {
        var result = new BsonDocument();
        foreach (var name in fields.Where(source.Contains))
        {
            var value = source[name];
            result[name == "_id" ? "id" : name] = value.IsObjectId ? value.ToString() : value;
        }

        return result;
    }
}
