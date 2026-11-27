using Knock.Hosting.Concurrency;
using Knock.Hosting.Data;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Reviews.Domain;
using Knock.Market.Api.Modules.Reviews.Dtos;
using Knock.Market.Api.Modules.Reviews.Validators;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Reviews.Data;

/// <summary>La visita que se califica, tal como la necesita el módulo (spec 051, R-12).</summary>
internal sealed record ReviewVisit(ObjectId Id, ObjectId RequestId, string ClientId, ObjectId TechnicianId, string State, DateTime? FinishedAtUtc);

/// <summary>Una opinión guardada, con lo mínimo para decidir quién puede responderla (R-15).</summary>
internal sealed record StoredReview(ObjectId Id, ObjectId TechnicianId, bool Answered);

/// <summary>Qué pasó al ocultar o mostrar una opinión (spec 061): no existe, cambió o ya estaba así.</summary>
internal enum ReviewModerationOutcome
{
    NotFound,
    Changed,
    Unchanged,
}

/// <summary>Resultado de <see cref="IReviewStore.SetHiddenAsync"/>; <c>TechnicianId</c> viene lleno si la opinión existe.</summary>
internal sealed record ReviewModerationResult(ReviewModerationOutcome Outcome, ObjectId? TechnicianId);

/// <summary>Opinión nueva ya validada y moderada. <c>Moderation</c> es el estado del comentario (R-17).</summary>
internal sealed record NewReview(
    ObjectId VisitId,
    ObjectId RequestId,
    ObjectId TechnicianId,
    string ClientId,
    int Stars,
    ReviewParts? Parts,
    string? Comment,
    IReadOnlyList<string> Photos,
    string Moderation,
    bool SaveAsTrusted);

/// <summary>Acceso a MongoDB de las opiniones (spec 051): colección <c>opiniones</c>, la reputación en <c>tecnicos</c> y el cierre en <c>visitas</c> y <c>solicitudes</c> de la misma base.</summary>
internal interface IReviewStore
{
    Task<ReviewVisit?> FindVisitAsync(ObjectId visitId, CancellationToken cancellationToken);

    Task<bool> IsTechnicianVisibleAsync(ObjectId technicianId, CancellationToken cancellationToken);

    /// <summary>Marca <c>anonimizada</c> las opiniones que escribió esa cuenta (spec 039, R-25). Repetirlo no cambia nada.</summary>
    Task AnonymizeByAuthorAsync(string clientId, CancellationToken cancellationToken);

    Task<ObjectId?> FindTechnicianByUserAsync(string sub, CancellationToken cancellationToken);

    Task<StoredReview?> FindReviewAsync(ObjectId id, CancellationToken cancellationToken);

    /// <summary>Guarda la opinión, pasa la solicitud a <c>calificada</c>, recalcula la reputación y escribe el evento en una transacción; <see langword="null"/> si la visita ya tenía opinión.</summary>
    Task<ObjectId?> CreateAsync(NewReview review, DateTime nowUtc, CancellationToken cancellationToken);

    /// <summary>Guarda la respuesta única del técnico; <see langword="false"/> si la opinión ya tenía respuesta.</summary>
    Task<bool> AnswerAsync(StoredReview review, string text, DateTime nowUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Oculta o muestra una opinión sin borrarla (spec 061): guarda quién, cuándo y por qué, recalcula la reputación y escribe <c>OpinionModerada</c> en una transacción. Los turnos de la misma opinión se
    /// serializan y quien entra después relee: repetir el mismo cambio no hace nada ni escribe otro evento.
    /// </summary>
    Task<ReviewModerationResult> SetHiddenAsync(ObjectId id, bool hidden, string? reason, string moderatorId, DateTime nowUtc, CancellationToken cancellationToken);

    Task<ReviewPage> ListAsync(ObjectId technicianId, (DateTime CreatedAtUtc, ObjectId Id)? cursor, int limit, CancellationToken cancellationToken);

    /// <summary>Marca que el plazo de 30 días de la visita terminó sin opinión y escribe el evento; repetirlo no hace nada.</summary>
    Task CloseOpinionPeriodAsync(ObjectId visitId, DateTime nowUtc, CancellationToken cancellationToken);
}

internal sealed class ReviewStore(
    [FromKeyedServices(ReviewsModuleExtensions.ModuleKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> options) : IReviewStore
{
    public const string CollectionName = "opiniones";
    private const string _id = "_id";
    private const string _technician = "tecnicoId";
    private const string _created = "creadoEn";
    private const string _stars = "calificacion";

    private readonly FindOptions _find = new() { MaxTime = options.Value.QueryMaxTime };

    private IMongoCollection<BsonDocument> Reviews => database.GetCollection<BsonDocument>(CollectionName);

    private IMongoCollection<BsonDocument> Visits => database.GetCollection<BsonDocument>("visitas");

    private IMongoCollection<BsonDocument> Technicians => database.GetCollection<BsonDocument>("tecnicos");

    public async Task<ReviewVisit?> FindVisitAsync(ObjectId visitId, CancellationToken cancellationToken)
    {
        var d = await Visits.Find(Builders<BsonDocument>.Filter.Eq(_id, visitId), _find).FirstOrDefaultAsync(cancellationToken);
        if (d is null)
        {
            return null;
        }

        DateTime? finishedAt = d.TryGetValue("terminadaEn", out var finished) && finished.IsBsonDateTime ? finished.ToUniversalTime() : null;
        return new ReviewVisit(visitId, d["solicitudId"].AsObjectId, d["clienteId"].AsString, d[_technician].AsObjectId, d["estado"].AsString, finishedAt);
    }

    public Task<bool> IsTechnicianVisibleAsync(ObjectId technicianId, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        return Technicians.Find(filters.Eq(_id, technicianId) & filters.Eq("oculto", false) & filters.Eq("estado", "activo"), _find).Limit(1).AnyAsync(cancellationToken);
    }

    public async Task<ObjectId?> FindTechnicianByUserAsync(string sub, CancellationToken cancellationToken)
    {
        var d = await Technicians.Find(Builders<BsonDocument>.Filter.Eq("usuarioId", sub), _find).Project(Builders<BsonDocument>.Projection.Include(_id)).FirstOrDefaultAsync(cancellationToken);
        return d?[_id].AsObjectId;
    }

    public async Task<StoredReview?> FindReviewAsync(ObjectId id, CancellationToken cancellationToken)
    {
        var d = await Reviews.Find(Builders<BsonDocument>.Filter.Eq(_id, id), _find).FirstOrDefaultAsync(cancellationToken);
        return d is null ? null : new StoredReview(id, d[_technician].AsObjectId, d.Contains("respuesta"));
    }

    public Task AnonymizeByAuthorAsync(string clientId, CancellationToken cancellationToken) =>
        Reviews.UpdateManyAsync(
            Builders<BsonDocument>.Filter.Eq("clienteId", clientId),
            Builders<BsonDocument>.Update.Set("anonimizada", true),
            cancellationToken: cancellationToken);

    public async Task<ObjectId?> CreateAsync(NewReview review, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(review);
        var id = ObjectId.GenerateNewId();
        var document = new BsonDocument
        {
            { _id, id },
            { "visitaId", review.VisitId },
            { _technician, review.TechnicianId },
            { "clienteId", review.ClientId },
            { _stars, review.Stars },
            { "fotos", new BsonArray(review.Photos) },
            { "moderacion", review.Moderation },
            { "oculta", false },
            { "anonimizada", false },
            { _created, nowUtc },
        };
        if (review.Parts is { } parts)
        {
            document.Add("partes", PartsOf(parts));
        }

        if (review.Comment is not null)
        {
            document.Add("comentario", review.Comment);
        }

        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        try
        {
            return await session.WithTransactionAsync<ObjectId?>(async (transaction, token) =>
            {
                await Reviews.InsertOneAsync(transaction, document, cancellationToken: token);
                await database.GetCollection<BsonDocument>("solicitudes").UpdateOneAsync(
                    transaction,
                    Builders<BsonDocument>.Filter.Eq(_id, review.RequestId) & Builders<BsonDocument>.Filter.Eq("estado", "terminada"),
                    Builders<BsonDocument>.Update.Set("estado", "calificada").Set("actualizadoEn", nowUtc),
                    cancellationToken: token);
                if (review.SaveAsTrusted)
                {
                    // «Guardar al técnico de confianza» (R-13): el mismo registro de la spec 042, idempotente por su índice único.
                    await database.GetCollection<BsonDocument>("tecnicos_confianza").UpdateOneAsync(
                        transaction,
                        Builders<BsonDocument>.Filter.Eq("clienteId", review.ClientId) & Builders<BsonDocument>.Filter.Eq(_technician, review.TechnicianId),
                        Builders<BsonDocument>.Update.SetOnInsert("guardadoEn", nowUtc),
                        new UpdateOptions { IsUpsert = true },
                        token);
                }

                await RecalculateAsync(transaction, review.TechnicianId, token);
                await AddEventAsync(transaction, ReviewRules.CreatedEvent, id.ToString(), review.VisitId, review.TechnicianId, nowUtc, token);
                return id;
            }, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return null;
        }
    }

    public async Task<bool> AnswerAsync(StoredReview review, string text, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(review);
        var filters = Builders<BsonDocument>.Filter;
        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        return await session.WithTransactionAsync(async (transaction, token) =>
        {
            var changed = await Reviews.UpdateOneAsync(
                transaction,
                filters.Eq(_id, review.Id) & filters.Eq(_technician, review.TechnicianId) & filters.Exists("respuesta", false),
                Builders<BsonDocument>.Update.Set("respuesta", new BsonDocument { { "texto", text }, { "fecha", nowUtc } }),
                cancellationToken: token);
            if (changed.MatchedCount != 1)
            {
                return false;
            }

            await AddEventAsync(transaction, ReviewRules.AnsweredEvent, review.Id.ToString(), null, review.TechnicianId, nowUtc, token);
            return true;
        }, cancellationToken: cancellationToken);
    }

    public Task<ReviewModerationResult> SetHiddenAsync(ObjectId id, bool hidden, string? reason, string moderatorId, DateTime nowUtc, CancellationToken cancellationToken) =>
        KeyedGate.RunAsync($"opinion:{id}", () => SetHiddenSerializedAsync(id, hidden, reason, moderatorId, nowUtc, cancellationToken), cancellationToken);

    private async Task<ReviewModerationResult> SetHiddenSerializedAsync(ObjectId id, bool hidden, string? reason, string moderatorId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        return await session.WithTransactionAsync(async (transaction, token) =>
        {
            var current = await Reviews.Find(transaction, filters.Eq(_id, id), _find).FirstOrDefaultAsync(token);
            if (current is null)
            {
                return new ReviewModerationResult(ReviewModerationOutcome.NotFound, null);
            }

            var technicianId = current[_technician].AsObjectId;
            var update = Builders<BsonDocument>.Update;
            var changed = await Reviews.UpdateOneAsync(
                transaction,
                filters.Eq(_id, id) & (hidden ? filters.Ne("oculta", true) : filters.Eq("oculta", true)),
                hidden
                    ? update.Set("oculta", true).Set("ocultaPor", moderatorId).Set("ocultaEn", nowUtc).Set("ocultaMotivo", reason)
                    : update.Set("oculta", false).Unset("ocultaPor").Unset("ocultaEn").Unset("ocultaMotivo"),
                cancellationToken: token);
            if (changed.ModifiedCount != 1)
            {
                return new ReviewModerationResult(ReviewModerationOutcome.Unchanged, technicianId);
            }

            await RecalculateAsync(transaction, technicianId, token);
            var owner = await Technicians.Find(transaction, filters.Eq(_id, technicianId), _find).Project(Builders<BsonDocument>.Projection.Include("usuarioId")).FirstOrDefaultAsync(token);
            var data = new BsonDocument
            {
                { "opinionId", id.ToString() },
                { _technician, technicianId.ToString() },
                { "accion", hidden ? ReviewRules.HiddenAction : ReviewRules.ShownAction },
                { "porId", moderatorId },
            };
            if (owner is not null && owner.TryGetValue("usuarioId", out var user) && user.IsString)
            {
                data.Add("tecnicoUsuarioId", user.AsString);
            }

            await database.GetCollection<BsonDocument>("outbox").InsertOneAsync(
                transaction,
                new BsonDocument { { _id, $"{ReviewRules.ModeratedEvent}:{id}:{nowUtc.Ticks}" }, { "tipo", ReviewRules.ModeratedEvent }, { "agregadoId", id.ToString() }, { "datos", data }, { "estado", "pendiente" }, { "intentos", 0 }, { _created, nowUtc } },
                cancellationToken: token);
            return new ReviewModerationResult(ReviewModerationOutcome.Changed, technicianId);
        }, cancellationToken: cancellationToken);
    }

    public async Task<ReviewPage> ListAsync(ObjectId technicianId, (DateTime CreatedAtUtc, ObjectId Id)? cursor, int limit, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var filter = filters.Eq(_technician, technicianId) & filters.Ne("oculta", true) & filters.Eq("moderacion", ReviewRules.Published);
        if (cursor is { } after)
        {
            filter &= filters.Lt(_created, after.CreatedAtUtc) | (filters.Eq(_created, after.CreatedAtUtc) & filters.Lt(_id, after.Id));
        }

        var page = await Reviews.Find(filter, _find).Sort(Builders<BsonDocument>.Sort.Descending(_created).Descending(_id)).Limit(limit + 1).ToListAsync(cancellationToken);
        var items = page.Take(limit).Select(ToItem).ToList();
        var next = page.Count > limit ? ReviewFormat.EncodeCursor(items[^1].CreadoEn, ObjectId.Parse(items[^1].Id)) : null;
        return new ReviewPage(items, await SummaryAsync(null, technicianId, cancellationToken), next);
    }

    public async Task CloseOpinionPeriodAsync(ObjectId visitId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        await session.WithTransactionAsync(async (transaction, token) =>
        {
            var changed = await Visits.UpdateOneAsync(
                transaction,
                Builders<BsonDocument>.Filter.Eq(_id, visitId) & Builders<BsonDocument>.Filter.Exists("periodoOpinionCerradoEn", false),
                Builders<BsonDocument>.Update.Set("periodoOpinionCerradoEn", nowUtc),
                cancellationToken: token);
            if (changed.MatchedCount == 1)
            {
                await AddEventAsync(transaction, ReviewRules.PeriodClosedEvent, visitId.ToString(), visitId, null, nowUtc, token);
            }

            return true;
        }, cancellationToken: cancellationToken);
    }

    private static BsonDocument PartsOf(ReviewParts parts)
    {
        var d = new BsonDocument();
        foreach (var (name, value) in new[] { ("puntualidad", parts.Puntualidad), ("calidad", parts.Calidad), ("precioJusto", parts.PrecioJusto), ("trato", parts.Trato) })
        {
            if (value is not null)
            {
                d.Add(name, value.Value);
            }
        }

        return d;
    }

    private static ReviewItem ToItem(BsonDocument d)
    {
        ReviewParts? parts = d.TryGetValue("partes", out var p) && p.IsBsonDocument
            ? new ReviewParts(Part(p.AsBsonDocument, "puntualidad"), Part(p.AsBsonDocument, "calidad"), Part(p.AsBsonDocument, "precioJusto"), Part(p.AsBsonDocument, "trato"))
            : null;
        var answer = d.TryGetValue("respuesta", out var r) && r.IsBsonDocument ? new ReviewAnswer(r["texto"].AsString, r["fecha"].ToUniversalTime()) : null;
        return new ReviewItem(
            d[_id].AsObjectId.ToString(),
            d[_stars].ToInt32(),
            parts,
            d.TryGetValue("comentario", out var comment) && comment.IsString ? comment.AsString : null,
            [.. d.GetValue("fotos", new BsonArray()).AsBsonArray.Select(x => x.AsString)],
            answer,
            d.GetValue("anonimizada", false).AsBoolean,
            true,
            d[_created].ToUniversalTime());
    }

    private static int? Part(BsonDocument d, string name) => d.TryGetValue(name, out var v) && v.IsNumeric ? v.ToInt32() : null;

    /// <summary>Distribución de las opiniones que cuentan (no ocultas), desde que se envían (D-04).</summary>
    private async Task<ReviewSummary> SummaryAsync(IClientSessionHandle? session, ObjectId technicianId, CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter.Eq(_technician, technicianId) & Builders<BsonDocument>.Filter.Ne("oculta", true);
        var groups = await (session is null ? Reviews.Aggregate() : Reviews.Aggregate(session))
            .Match(filter)
            .Group(new BsonDocument { { _id, "$" + _stars }, { "n", new BsonDocument("$sum", 1) } })
            .ToListAsync(cancellationToken);
        var distribution = Enumerable.Range(1, 5).ToDictionary(n => n, _ => 0);
        foreach (var g in groups)
        {
            if (g[_id].ToInt32() is >= 1 and <= 5 and var star)
            {
                distribution[star] = g["n"].ToInt32();
            }
        }

        return new ReviewSummary(
            ReviewRules.AverageOf(distribution),
            distribution.Values.Sum(),
            distribution.ToDictionary(x => x.Key.ToString(System.Globalization.CultureInfo.InvariantCulture), x => x.Value));
    }

    /// <summary>Guarda en el perfil del técnico la reputación ya calculada (R-14): promedio, total, distribución y nivel. Lo lee la spec 046 (TEC-13).</summary>
    private async Task RecalculateAsync(IClientSessionHandle session, ObjectId technicianId, CancellationToken cancellationToken)
    {
        var summary = await SummaryAsync(session, technicianId, cancellationToken);
        var reputation = new BsonDocument
        {
            { "promedio", summary.Promedio is { } average ? new BsonDouble(average) : BsonNull.Value },
            { "total", summary.Total },
            { "distribucion", new BsonDocument(summary.Distribucion.Select(x => new BsonElement(x.Key, x.Value))) },
            { "nivel", ReviewRules.LevelOf(summary.Total, summary.Promedio ?? 0) },
        };
        await Technicians.UpdateOneAsync(session, Builders<BsonDocument>.Filter.Eq(_id, technicianId), ReputationUpdate(reputation, summary), cancellationToken: cancellationToken);
    }

    /// <summary>La reputación y, en la misma escritura, <c>calificacion</c> y <c>totalResenas</c>: los campos por los que ordenan y que muestran la búsqueda (specs 027 y 028), la lista, Explorar y el mapa
    /// (spec 046). Sin opiniones visibles no hay <c>calificacion</c>, igual que un técnico nuevo.</summary>
    private static UpdateDefinition<BsonDocument> ReputationUpdate(BsonDocument reputation, ReviewSummary summary)
    {
        var update = Builders<BsonDocument>.Update.Set("reputacion", reputation).Set("totalResenas", summary.Total);
        return summary.Promedio is { } average ? update.Set(_stars, average) : update.Unset(_stars);
    }

    /// <summary>Buzón de salida en la misma transacción; el evento solo lleva identificadores (R-19).</summary>
    private Task AddEventAsync(IClientSessionHandle session, string type, string aggregateId, ObjectId? visitId, ObjectId? technicianId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var data = new BsonDocument();
        if (visitId is not null)
        {
            data.Add("visitaId", visitId.Value.ToString());
        }

        if (technicianId is not null)
        {
            data.Add(_technician, technicianId.Value.ToString());
        }

        return database.GetCollection<BsonDocument>("outbox").InsertOneAsync(
            session,
            new BsonDocument { { _id, $"{type}:{aggregateId}" }, { "tipo", type }, { "agregadoId", aggregateId }, { "datos", data }, { "estado", "pendiente" }, { "intentos", 0 }, { _created, nowUtc } },
            cancellationToken: cancellationToken);
    }
}
