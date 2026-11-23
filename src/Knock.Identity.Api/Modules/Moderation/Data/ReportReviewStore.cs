using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Modules.Moderation.Domain;
using Knock.Identity.Api.Modules.Panel;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Identity.Api.Modules.Moderation.Data;

/// <summary>Un reporte en la cola: motivo y texto, nunca las personas ni sus datos (spec 053, R-19).</summary>
internal sealed record QueuedReport(ObjectId Id, string Reason, string Text, string? Severity, string State, DateTime CreatedAt, int Rank, string? Priority = null);

/// <summary>Las dos partes del reporte decidido. Solo las usa el servicio para avisar a cada una sin revelar a la otra y para impedir que el moderador decida un reporte que lo involucra.</summary>
internal sealed record DecidedReportParties(string ReporterId, string ReportedId, string Reason);

/// <summary>La decisión sobre un reporte (R-20). <see cref="Response"/> se deriva de la gravedad (D-06).</summary>
internal sealed record ReportDecisionRecord(ObjectId Id, string Severity, string Response, string Reason, string DeciderSub, DateTime DecidedAt);

/// <summary>Resultado de decidir: el reporte no existe, ya estaba decidido o se decidió y trae a las partes.</summary>
internal sealed record ReportDecisionStoreResult(DecisionStoreOutcome Outcome, DecidedReportParties? Parties = null)
{
    public sealed override string ToString() => nameof(ReportDecisionStoreResult);
}

/// <summary>Lo que el panel necesita de <c>reportes</c>; existe para probar el servicio con dobles.</summary>
internal interface IReportReviewStore
{
    Task<IReadOnlyList<QueuedReport>> ListAsync(string? severity, IReadOnlyList<string> states, DateTime from, DateTime to, ReviewCursor? after, int take, CancellationToken cancellationToken);

    /// <summary>Las partes de un reporte pendiente, para negar la decisión si el moderador es una de ellas (spec 059, R-07). <see langword="null"/> si no existe.</summary>
    Task<DecidedReportParties?> FindPartiesAsync(ObjectId id, CancellationToken cancellationToken);

    /// <summary>
    /// En una sola transacción: guarda la decisión de un reporte <c>abierto</c> o <c>en_apelacion</c>, ejecuta <paramref name="applyEffects"/> (la respuesta de la gravedad; devuelve la prioridad derivada, que se guarda aparte sin tocar la gravedad) y la auditoría
    /// <paramref name="writeAudit"/>; si cualquiera falla no queda nada (R-22).
    /// </summary>
    Task<ReportDecisionStoreResult> DecideAsync(
        ReportDecisionRecord decision,
        Func<StoreTransaction, DecidedReportParties, Task<string>> applyEffects,
        Func<StoreTransaction, Task> writeAudit,
        CancellationToken cancellationToken);
}

/// <summary>
/// Cola y decisión de <c>reportes</c> para el panel (spec 053, REP-04 y REP-05). La colección es de Safety (spec 043). El orden es por gravedad (crítica primero; sin gravedad al final) y antigüedad:
/// MongoDB no ordena por una lista cerrada, así que la posición se calcula en la consulta (<c>$switch</c>) y el volumen del piloto no necesita un campo guardado.
/// </summary>
internal sealed class ReportReviewStore(
    [FromKeyedServices(DataExtensions.DatabaseKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> mongoOptions) : IReportReviewStore
{
    public const string Collection = "reportes";
    private const string _idField = "_id";
    private const string _stateField = "estado";
    private const string _createdField = "creadoEn";
    private const string _severityField = "gravedad";
    private const string _rankField = "orden";
    private const string _priorityField = "prioridad";

    private static readonly string[] _pendingStates = ["abierto", "en_apelacion"];
    private static readonly FilterDefinitionBuilder<BsonDocument> _filter = Builders<BsonDocument>.Filter;

    private IMongoCollection<BsonDocument> Reports => database.GetCollection<BsonDocument>(Collection);

    private static BsonDocument RankExpression()
    {
        var branches = new BsonArray(ReportSeverities.All.Select((severity, index) => new BsonDocument
        {
            ["case"] = new BsonDocument("$eq", new BsonArray { "$" + _severityField, severity }),
            ["then"] = index,
        }));
        return new BsonDocument("$switch", new BsonDocument { ["branches"] = branches, ["default"] = ReportSeverities.All.Count });
    }

    public async Task<IReadOnlyList<QueuedReport>> ListAsync(string? severity, IReadOnlyList<string> states, DateTime from, DateTime to, ReviewCursor? after, int take, CancellationToken cancellationToken)
    {
        var match = _filter.In(_stateField, states) & _filter.Gte(_createdField, from) & _filter.Lte(_createdField, to);
        if (severity is not null)
        {
            match &= _filter.Eq(_severityField, severity);
        }

        var pipeline = Reports.Aggregate(new AggregateOptions { MaxTime = mongoOptions.Value.QueryMaxTime })
            .Match(match)
            .AppendStage<BsonDocument>(new BsonDocument("$addFields", new BsonDocument(_rankField, RankExpression())));
        if (after is { } cursor)
        {
            var rank = new BsonDocument("$gt", new BsonArray { "$" + _rankField, cursor.Rank });
            var sameRank = new BsonDocument("$eq", new BsonArray { "$" + _rankField, cursor.Rank });
            var older = new BsonDocument("$gt", new BsonArray { "$" + _createdField, cursor.At });
            var sameTime = new BsonDocument("$eq", new BsonArray { "$" + _createdField, cursor.At });
            var laterId = new BsonDocument("$gt", new BsonArray { "$" + _idField, cursor.Id });
            var expression = new BsonDocument("$or", new BsonArray
            {
                rank,
                new BsonDocument("$and", new BsonArray { sameRank, new BsonDocument("$or", new BsonArray { older, new BsonDocument("$and", new BsonArray { sameTime, laterId }) }) }),
            });
            pipeline = pipeline.AppendStage<BsonDocument>(new BsonDocument("$match", new BsonDocument("$expr", expression)));
        }

        var documents = await pipeline
            .Sort(Builders<BsonDocument>.Sort.Ascending(_rankField).Ascending(_createdField).Ascending(_idField))
            .Limit(take)
            .Project(Builders<BsonDocument>.Projection.Include("motivo").Include("descripcion").Include(_severityField).Include(_stateField).Include(_createdField).Include(_rankField).Include(_priorityField))
            .ToListAsync(cancellationToken);
        return [.. documents.Select(d => new QueuedReport(
            d[_idField].AsObjectId,
            d["motivo"].AsString,
            d["descripcion"].AsString,
            d.TryGetValue(_severityField, out var s) && s.IsString ? s.AsString : null,
            d[_stateField].AsString,
            d[_createdField].ToUniversalTime(),
            d[_rankField].ToInt32(),
            d.TryGetValue(_priorityField, out var p) && p.IsString ? p.AsString : null))];
    }

    public async Task<DecidedReportParties?> FindPartiesAsync(ObjectId id, CancellationToken cancellationToken)
    {
        var d = await Reports.Find(_filter.Eq(_idField, id), new FindOptions { MaxTime = mongoOptions.Value.QueryMaxTime })
            .Project(Builders<BsonDocument>.Projection.Include("reportaId").Include("reportadoId").Include("motivo")).Limit(1).FirstOrDefaultAsync(cancellationToken);
        return d is null ? null : new DecidedReportParties(d["reportaId"].AsString, d["reportadoId"].AsString, d["motivo"].AsString);
    }

    public async Task<ReportDecisionStoreResult> DecideAsync(
        ReportDecisionRecord decision,
        Func<StoreTransaction, DecidedReportParties, Task<string>> applyEffects,
        Func<StoreTransaction, Task> writeAudit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(applyEffects);
        ArgumentNullException.ThrowIfNull(writeAudit);
        return await DecisionTurns.RunAsync(
            decision.Id.ToString(),
            () => SettledAsync(decision.Id, cancellationToken),
            outcome => new ReportDecisionStoreResult(outcome),
            () => DecideInTransactionAsync(decision, applyEffects, writeAudit, cancellationToken),
            cancellationToken);
    }

    private async Task<DecisionStoreOutcome?> SettledAsync(ObjectId id, CancellationToken cancellationToken)
    {
        var state = await Reports.Find(_filter.Eq(_idField, id), new FindOptions { MaxTime = mongoOptions.Value.QueryMaxTime })
            .Project(Builders<BsonDocument>.Projection.Include(_stateField)).Limit(1).FirstOrDefaultAsync(cancellationToken);
        if (state is null)
        {
            return DecisionStoreOutcome.NotFound;
        }

        return _pendingStates.Contains(state[_stateField].AsString) ? null : DecisionStoreOutcome.AlreadyDecided;
    }

    private async Task<ReportDecisionStoreResult> DecideInTransactionAsync(
        ReportDecisionRecord decision,
        Func<StoreTransaction, DecidedReportParties, Task<string>> applyEffects,
        Func<StoreTransaction, Task> writeAudit,
        CancellationToken cancellationToken)
    {
        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        return await session.WithTransactionAsync(
            async (transaction, token) =>
            {
                // Una apelación de suspensión (spec 060) se resuelve por /admin/apelaciones/{id}/resolucion, no con una decisión nueva de la gravedad.
                var pending = _filter.Eq(_idField, decision.Id) & _filter.In(_stateField, _pendingStates) & _filter.Ne("apelacion.origen", "suspension");
                var update = Builders<BsonDocument>.Update
                    .Set(_severityField, decision.Severity)
                    .Set(_stateField, "decidido")
                    .Set("decision", new BsonDocument
                    {
                        ["gravedad"] = decision.Severity,
                        ["respuesta"] = decision.Response,
                        ["motivo"] = decision.Reason,
                        ["decididoPor"] = decision.DeciderSub,
                        ["decididoEn"] = decision.DecidedAt,
                    });
                var matched = await Reports.FindOneAndUpdateAsync(
                    transaction,
                    pending,
                    update,
                    new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After, Projection = Builders<BsonDocument>.Projection.Include("reportaId").Include("reportadoId").Include("motivo") },
                    token);
                if (matched is null)
                {
                    var exists = await Reports.Find(transaction, _filter.Eq(_idField, decision.Id)).Limit(1).AnyAsync(token);
                    return new ReportDecisionStoreResult(exists ? DecisionStoreOutcome.AlreadyDecided : DecisionStoreOutcome.NotFound);
                }

                var parties = new DecidedReportParties(matched["reportaId"].AsString, matched["reportadoId"].AsString, matched["motivo"].AsString);
                var priority = await applyEffects(new StoreTransaction(transaction), parties);

                // La prioridad se deriva aparte: `gravedad` y `decision.gravedad` quedan como las asignó el moderador (spec 053, R-20).
                await Reports.UpdateOneAsync(transaction, _filter.Eq(_idField, decision.Id), Builders<BsonDocument>.Update.Set(_priorityField, priority), cancellationToken: token);
                await writeAudit(new StoreTransaction(transaction));
                return new ReportDecisionStoreResult(DecisionStoreOutcome.Decided, parties);
            },
            cancellationToken: cancellationToken);
    }
}
