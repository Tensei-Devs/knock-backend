using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Requests.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Requests.Data;

/// <summary>
/// Proyección de la suspensión preventiva en Mercado (spec 059, R-10, R-11 y R-19). Cada evento corre en UNA transacción: registra su <c>eventoId</c> (idempotencia), cambia el <c>estado</c>
/// del perfil del técnico con una operación condicionada y, al suspender, deja un aviso por cita próxima en el <c>outbox</c> hacia Mensajería. Lo ya cotizado no se borra y las visitas no se
/// cancelan (R-12). Los avisos llevan solo identificadores. Único punto del módulo con acceso a MongoDB para este flujo.
/// </summary>
internal sealed class AccountEventStore(
    [FromKeyedServices(RequestsModuleExtensions.ModuleKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> mongoOptions,
    IOutbox outbox,
    TimeProvider clock)
{
    public const string ReceivedCollection = "eventos_recibidos";

    private readonly FindOptions _findOptions = new() { MaxTime = mongoOptions.Value.QueryMaxTime };

    /// <summary>Dos entregas simultáneas del mismo evento chocan en el <c>_id</c> de <c>eventos_recibidos</c>: la que pierde ya no tiene nada que aplicar (R-19).</summary>
    public async Task<AccountEventOutcome> ApplyAsync(string eventId, string type, string userId, CancellationToken cancellationToken)
    {
        try
        {
            return await ApplyInTransactionAsync(eventId, type, userId, cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return AccountEventOutcome.Duplicate;
        }
    }

    private Task<AccountEventOutcome> ApplyInTransactionAsync(string eventId, string type, string userId, CancellationToken cancellationToken) =>
        MarketTransactions.RunAsync(
            database,
            async (transaction, token) =>
            {
                var now = clock.GetUtcNow().UtcDateTime;
                var received = database.GetCollection<BsonDocument>(ReceivedCollection);
                if (await received.Find(transaction, Builders<BsonDocument>.Filter.Eq("_id", eventId), _findOptions).AnyAsync(token))
                {
                    return AccountEventOutcome.Duplicate;
                }

                await received.InsertOneAsync(transaction, new BsonDocument { { "_id", eventId }, { "tipo", type }, { "recibidoEn", now } }, cancellationToken: token);
                var suspending = type == AccountEventRules.SuspendedEvent;
                var f = Builders<BsonDocument>.Filter;
                await database.GetCollection<BsonDocument>("tecnicos").UpdateOneAsync(
                    transaction,
                    f.Eq("usuarioId", userId) & f.Eq("estado", suspending ? AccountEventRules.ActiveTechnicianState : AccountEventRules.SuspendedTechnicianState),
                    Builders<BsonDocument>.Update.Set("estado", suspending ? AccountEventRules.SuspendedTechnicianState : AccountEventRules.ActiveTechnicianState),
                    cancellationToken: token);
                if (suspending)
                {
                    await NotifyUpcomingVisitsAsync(transaction, eventId, userId, now, token);
                }

                return AccountEventOutcome.Applied;
            },
            AccountEventOutcome.Conflict,
            cancellationToken);

    private async Task NotifyUpcomingVisitsAsync(IClientSessionHandle transaction, string eventId, string technicianUserId, DateTime now, CancellationToken cancellationToken)
    {
        var f = Builders<BsonDocument>.Filter;
        var visits = await database.GetCollection<BsonDocument>(ChoiceStore.VisitsCollection)
            .Find(transaction, f.Eq("tecnicoUsuarioId", technicianUserId) & f.Eq("estado", VisitRules.Confirmed), _findOptions)
            .Project(Builders<BsonDocument>.Projection.Include("clienteId"))
            .ToListAsync(cancellationToken);
        foreach (var visit in visits.Where(v => v.TryGetValue("clienteId", out var client) && client.IsString))
        {
            var visitId = visit["_id"].AsObjectId.ToString();
            var payload = new BsonDocument
            {
                { "eventoId", $"{AccountEventRules.AffectedVisitNotice}:{eventId}:{visitId}" },
                { "destinatarioId", visit["clienteId"].AsString },
                { "rol", AccountEventRules.ClientRole },
                { "visitaId", visitId },
            };
            await outbox.AddAsync(transaction, OutboxMessage.Create(AccountEventRules.AffectedVisitNotice, $"{eventId}:{visitId}", payload, now), cancellationToken);
        }
    }
}
