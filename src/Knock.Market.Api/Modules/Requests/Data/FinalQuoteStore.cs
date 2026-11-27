using Knock.Market.Api.Modules.Requests.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Requests.Data;

/// <summary>Cotización aceptada vista por el técnico que quiere pedir un ajuste (spec 049, R-15).</summary>
internal sealed record QuoteForAdjustment(ObjectId Id, ObjectId RequestId, string TechnicianUserId, string State, int Cap, string PriceType, bool HasPendingAdjustment);

/// <summary>Ajuste de precio tal como se guarda (spec 049, §6).</summary>
internal sealed record AdjustmentRecord(ObjectId Id, ObjectId QuoteId, ObjectId VisitId, string ClientId, string TechnicianUserId, int NewAmount, string State, DateTime ExpiresAtUtc);

/// <summary>Cotización final embebida en la visita, tal como se guarda (spec 049, §6), para su lectura (spec 063).</summary>
internal sealed record FinalQuoteRead(int Price, string Description, string State, DateTime ExpiresAtUtc, DateTime? DecidedAtUtc);

/// <summary>Ajuste pendiente tal como se lista (spec 063, R-04).</summary>
internal sealed record PendingAdjustmentRead(ObjectId Id, ObjectId QuoteId, int NewAmount, string Reason, string State, DateTime ExpiresAtUtc);

/// <summary>
/// Cotización final de la visita y ajustes de precio (spec 049): cada cambio de estado es una actualización condicionada, escrita junto con su evento del buzón y, si
/// corresponde, con la tarea de vencimiento, en la misma transacción. Los eventos solo llevan identificadores (R-17). Único punto del módulo con acceso a MongoDB para estas operaciones.
/// </summary>
internal sealed class FinalQuoteStore([FromKeyedServices(RequestsModuleExtensions.ModuleKey)] IMongoDatabase database, IOutbox outbox)
{
    public const string AdjustmentsCollection = "ajustes_precio";
    private const string _stateField = "estado";
    private const string _updatedField = "actualizadoEn";
    private const string _finalQuoteState = "cotizacionFinal.estado";
    private const string _finalQuoteExpires = "cotizacionFinal.venceEn";
    private const string _pendingAdjustmentField = "ajustePendienteId";

    private IMongoCollection<BsonDocument> Visits => database.GetCollection<BsonDocument>(ChoiceStore.VisitsCollection);

    private IMongoCollection<BsonDocument> Quotes => database.GetCollection<BsonDocument>(RequestsStore.QuotesCollection);

    private IMongoCollection<BsonDocument> Adjustments => database.GetCollection<BsonDocument>(AdjustmentsCollection);

    /// <summary>La cotización final guardada en la visita, o <see langword="null"/> si la visita no la tiene (spec 063, R-01). Solo lee.</summary>
    public async Task<FinalQuoteRead?> FindFinalQuoteAsync(ObjectId visitId, CancellationToken cancellationToken)
    {
        var found = await Visits.Find(Builders<BsonDocument>.Filter.Eq("_id", visitId)).Project(Builders<BsonDocument>.Projection.Include("cotizacionFinal")).FirstOrDefaultAsync(cancellationToken);
        if (found is null || !found.TryGetValue("cotizacionFinal", out var value) || !value.IsBsonDocument)
        {
            return null;
        }

        var d = value.AsBsonDocument;
        return new FinalQuoteRead(
            d["precio"].ToInt32(),
            d["descripcion"].AsString,
            d["estado"].AsString,
            d["venceEn"].ToUniversalTime(),
            d.TryGetValue("decididaEn", out var decided) && decided.IsValidDateTime ? decided.ToUniversalTime() : null);
    }

    /// <summary>Los ajustes <c>pendiente</c> y vigentes de la visita, del que vence antes al que vence después (spec 063, R-04 y R-05). Solo lee.</summary>
    public async Task<IReadOnlyList<PendingAdjustmentRead>> ListPendingAdjustmentsAsync(ObjectId visitId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter;
        var found = await Adjustments
            .Find(filter.Eq("visitaId", visitId) & filter.Eq(_stateField, FinalQuoteRules.Pending) & filter.Gt("venceEn", nowUtc))
            .Sort(Builders<BsonDocument>.Sort.Ascending("venceEn"))
            .ToListAsync(cancellationToken);
        return [.. found.Select(d => new PendingAdjustmentRead(d["_id"].AsObjectId, d["cotizacionId"].AsObjectId, d["montoNuevo"].ToInt32(), d["motivo"].AsString, d[_stateField].AsString, d["venceEn"].ToUniversalTime()))];
    }

    public async Task<string?> QuoteTypeAsync(ObjectId quoteId, CancellationToken cancellationToken)
    {
        var found = await Quotes.Find(Builders<BsonDocument>.Filter.Eq("_id", quoteId)).Project(Builders<BsonDocument>.Projection.Include("tipoPrecio")).FirstOrDefaultAsync(cancellationToken);
        return found is not null && found.TryGetValue("tipoPrecio", out var type) && type.IsString ? type.AsString : null;
    }

    /// <summary>
    /// Guarda la cotización final en la visita <c>en_curso</c> del técnico, solo si no tiene ya una (R-13), y programa su vencimiento a las 48 horas. <see langword="null"/> si otra
    /// petición o el estado de la visita lo impiden.
    /// </summary>
    public async Task<DateTime?> CreateFinalQuoteAsync(VisitRecord visit, int price, string description, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(visit);
        var expires = FinalQuoteRules.FinalQuoteExpiresAt(nowUtc);
        var created = await MarketTransactions.RunAsync(database, async (transaction, token) =>
        {
            var filter = Builders<BsonDocument>.Filter;
            var changed = await Visits.UpdateOneAsync(
                transaction,
                filter.Eq("_id", visit.Id) & filter.Eq("tecnicoUsuarioId", visit.TechnicianUserId) & filter.Eq(_stateField, VisitRules.InProgress) & filter.Exists("cotizacionFinal", false),
                Builders<BsonDocument>.Update
                    .Set("cotizacionFinal", new BsonDocument { { "precio", price }, { "descripcion", description }, { "estado", FinalQuoteRules.Sent }, { "venceEn", expires }, { "creadoEn", nowUtc } })
                    .Set(_updatedField, nowUtc),
                cancellationToken: token);
            if (changed.MatchedCount != 1)
            {
                return false;
            }

            await outbox.AddAsync(transaction, OutboxMessage.Create("CotizacionFinalEnviada", visit.Id.ToString(), VisitStore.Identifiers(visit), nowUtc), token);
            await new TareasProgramadasStore(database).ScheduleByKeyAsync(transaction, FinalQuoteRules.FinalQuoteExpiryTask, visit.Id.ToString(), expires, nowUtc, token);
            return true;
        }, false, cancellationToken);
        return created ? expires : null;
    }

    /// <summary>
    /// El cliente dueño decide la cotización final pendiente y vigente (R-14). Al rechazar, la visita queda <c>terminada</c> con <c>resultado = solo_visita</c> y se escribe
    /// <c>VisitaCerrada</c> en la misma transacción. <see langword="false"/> si ya no está pendiente, venció o la ganó otra petición.
    /// </summary>
    public Task<bool> DecideFinalQuoteAsync(VisitRecord visit, bool approve, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(visit);
        var state = approve ? FinalQuoteRules.Approved : FinalQuoteRules.Rejected;
        var filter = Builders<BsonDocument>.Filter;
        return CloseFinalQuoteAsync(
            visit,
            filter.Eq("clienteId", visit.ClientId) & filter.Eq(_finalQuoteState, FinalQuoteRules.Sent) & filter.Gt(_finalQuoteExpires, nowUtc),
            state,
            "CotizacionFinalDecidida",
            nowUtc,
            cancellationToken);
    }

    /// <summary>Vence una cotización final sin respuesta: se trata como rechazada por vencimiento (R-14). Idempotente; <see langword="false"/> si ya no estaba pendiente o aún no vence.</summary>
    public async Task<bool> ExpireFinalQuoteAsync(ObjectId visitId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var found = await Visits.Find(Builders<BsonDocument>.Filter.Eq("_id", visitId)).Limit(1).ToListAsync(cancellationToken);
        if (found.Count == 0)
        {
            return false;
        }

        var visit = VisitStore.ToRecord(found[0]);
        var filter = Builders<BsonDocument>.Filter;
        return await CloseFinalQuoteAsync(visit, filter.Eq(_finalQuoteState, FinalQuoteRules.Sent) & filter.Lte(_finalQuoteExpires, nowUtc), FinalQuoteRules.Expired, "CotizacionFinalVencida", nowUtc, cancellationToken);
    }

    public async Task<QuoteForAdjustment?> FindQuoteAsync(ObjectId quoteId, CancellationToken cancellationToken)
    {
        var found = await Quotes.Find(Builders<BsonDocument>.Filter.Eq("_id", quoteId)).Limit(1).ToListAsync(cancellationToken);
        if (found.Count == 0)
        {
            return null;
        }

        var d = found[0];
        return new QuoteForAdjustment(
            d["_id"].AsObjectId,
            d["solicitudId"].AsObjectId,
            d["tecnicoUsuarioId"].AsString,
            d[_stateField].AsString,
            d["tope"].ToInt32(),
            d["tipoPrecio"].AsString,
            d.Contains(_pendingAdjustmentField));
    }

    /// <summary>La visita de una cotización elegida (una por cotización), o <see langword="null"/>.</summary>
    public async Task<ObjectId?> FindVisitIdByQuoteAsync(ObjectId quoteId, CancellationToken cancellationToken)
    {
        var found = await Visits.Find(Builders<BsonDocument>.Filter.Eq("cotizacionId", quoteId)).Project(Builders<BsonDocument>.Projection.Include("_id")).FirstOrDefaultAsync(cancellationToken);
        return found?["_id"].AsObjectId;
    }

    /// <summary>
    /// Crea el ajuste pendiente (R-15): la visita debe estar <c>en_curso</c>, la cotización <c>aceptada</c>, del técnico, con el mismo tope que vio y sin otro ajuste pendiente,
    /// en una sola transacción con su evento y su vencimiento a las 24 horas. <see langword="null"/> si algo de eso ya no se cumple.
    /// </summary>
    public async Task<(ObjectId Id, DateTime ExpiresAtUtc)?> CreateAdjustmentAsync(QuoteForAdjustment quote, VisitRecord visit, int newAmount, string reason, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(quote);
        ArgumentNullException.ThrowIfNull(visit);
        var id = ObjectId.GenerateNewId();
        var expires = FinalQuoteRules.AdjustmentExpiresAt(nowUtc);
        var created = await MarketTransactions.RunAsync(database, async (transaction, token) =>
        {
            var filter = Builders<BsonDocument>.Filter;
            var visitTouched = await Visits.UpdateOneAsync(
                transaction,
                filter.Eq("_id", visit.Id) & filter.Eq(_stateField, VisitRules.InProgress),
                Builders<BsonDocument>.Update.Set(_updatedField, nowUtc),
                cancellationToken: token);
            if (visitTouched.MatchedCount != 1)
            {
                return false;
            }

            var quoteMarked = await Quotes.UpdateOneAsync(
                transaction,
                filter.Eq("_id", quote.Id) & filter.Eq("tecnicoUsuarioId", quote.TechnicianUserId) & filter.Eq(_stateField, "aceptada") & filter.Eq("tope", quote.Cap) & filter.Exists(_pendingAdjustmentField, false),
                Builders<BsonDocument>.Update.Set(_pendingAdjustmentField, id).Set(_updatedField, nowUtc),
                cancellationToken: token);
            if (quoteMarked.MatchedCount != 1)
            {
                // La visita solo recibió una marca de actualización; no hay nada más que deshacer.
                return false;
            }

            await Adjustments.InsertOneAsync(
                transaction,
                new BsonDocument
                {
                    { "_id", id },
                    { "visitaId", visit.Id },
                    { "cotizacionId", quote.Id },
                    { "clienteId", visit.ClientId },
                    { "tecnicoUsuarioId", visit.TechnicianUserId },
                    { "topeAnterior", quote.Cap },
                    { "montoNuevo", newAmount },
                    { "motivo", reason },
                    { _stateField, FinalQuoteRules.Pending },
                    { "venceEn", expires },
                    { "creadoEn", nowUtc },
                },
                cancellationToken: token);
            await outbox.AddAsync(transaction, OutboxMessage.Create("AjustePrecioPropuesto", id.ToString(), AdjustmentIdentifiers(id, quote.Id, visit), nowUtc), token);
            await new TareasProgramadasStore(database).ScheduleByKeyAsync(transaction, FinalQuoteRules.AdjustmentExpiryTask, id.ToString(), expires, nowUtc, token);
            return true;
        }, false, cancellationToken);
        return created ? (id, expires) : null;
    }

    public async Task<AdjustmentRecord?> FindAdjustmentAsync(ObjectId id, CancellationToken cancellationToken)
    {
        var found = await Adjustments.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).Limit(1).ToListAsync(cancellationToken);
        if (found.Count == 0)
        {
            return null;
        }

        var d = found[0];
        return new AdjustmentRecord(d["_id"].AsObjectId, d["cotizacionId"].AsObjectId, d["visitaId"].AsObjectId, d["clienteId"].AsString, d["tecnicoUsuarioId"].AsString, d["montoNuevo"].ToInt32(), d[_stateField].AsString, d["venceEn"].ToUniversalTime());
    }

    /// <summary>
    /// El cliente decide el ajuste pendiente y vigente (R-16). Si aprueba, el tope de la cotización y el de la visita pasan al monto nuevo; si rechaza, el precio sigue siendo el tope aprobado.
    /// <see langword="false"/> si ya fue decidido, venció o lo ganó otra petición.
    /// </summary>
    public Task<bool> DecideAdjustmentAsync(AdjustmentRecord adjustment, bool approve, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(adjustment);
        var filter = Builders<BsonDocument>.Filter;
        return ResolveAdjustmentAsync(
            adjustment,
            filter.Eq("clienteId", adjustment.ClientId) & filter.Gt("venceEn", nowUtc),
            approve ? FinalQuoteRules.AdjustmentApproved : FinalQuoteRules.AdjustmentRejected,
            "AjustePrecioDecidido",
            nowUtc,
            cancellationToken);
    }

    /// <summary>Vence un ajuste sin respuesta: el precio no cambia (R-16). Idempotente.</summary>
    public async Task<bool> ExpireAdjustmentAsync(ObjectId id, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var adjustment = await FindAdjustmentAsync(id, cancellationToken);
        return adjustment is not null
            && await ResolveAdjustmentAsync(adjustment, Builders<BsonDocument>.Filter.Lte("venceEn", nowUtc), FinalQuoteRules.AdjustmentExpired, "AjustePrecioVencido", nowUtc, cancellationToken);
    }

    private Task<bool> CloseFinalQuoteAsync(VisitRecord visit, FilterDefinition<BsonDocument> condition, string newState, string eventType, DateTime nowUtc, CancellationToken cancellationToken) =>
        MarketTransactions.RunAsync(database, async (transaction, token) =>
        {
            var filter = Builders<BsonDocument>.Filter;
            var update = Builders<BsonDocument>.Update.Set(_finalQuoteState, newState).Set("cotizacionFinal.decididaEn", nowUtc).Set(_updatedField, nowUtc);
            var closes = newState != FinalQuoteRules.Approved;
            if (closes)
            {
                update = update.Set(_stateField, "terminada").Set("terminadaEn", nowUtc).Set("resultado", FinalQuoteRules.OnlyVisitResult);
            }

            var changed = await Visits.UpdateOneAsync(transaction, filter.Eq("_id", visit.Id) & filter.Eq(_stateField, VisitRules.InProgress) & condition, update, cancellationToken: token);
            if (changed.MatchedCount != 1)
            {
                return false;
            }

            await outbox.AddAsync(transaction, OutboxMessage.Create(eventType, visit.Id.ToString(), VisitStore.Identifiers(visit), nowUtc), token);
            if (closes)
            {
                await outbox.AddAsync(transaction, VisitStore.ClosedMessage(visit.Id, wasBlocked: false, new BsonDocument { { "visitaId", visit.Id.ToString() }, { "motivo", "terminada" } }, nowUtc), token);
            }

            return true;
        }, false, cancellationToken);

    private Task<bool> ResolveAdjustmentAsync(AdjustmentRecord adjustment, FilterDefinition<BsonDocument> condition, string newState, string eventType, DateTime nowUtc, CancellationToken cancellationToken) =>
        MarketTransactions.RunAsync(database, async (transaction, token) =>
        {
            var filter = Builders<BsonDocument>.Filter;
            var changed = await Adjustments.UpdateOneAsync(
                transaction,
                filter.Eq("_id", adjustment.Id) & filter.Eq(_stateField, FinalQuoteRules.Pending) & condition,
                Builders<BsonDocument>.Update.Set(_stateField, newState).Set("decididoEn", nowUtc),
                cancellationToken: token);
            if (changed.MatchedCount != 1)
            {
                return false;
            }

            var quoteUpdate = Builders<BsonDocument>.Update.Unset(_pendingAdjustmentField).Set(_updatedField, nowUtc);
            if (newState == FinalQuoteRules.AdjustmentApproved)
            {
                quoteUpdate = quoteUpdate.Set("tope", adjustment.NewAmount).Set("ajusteAprobadoId", adjustment.Id);
                await Visits.UpdateOneAsync(transaction, filter.Eq("_id", adjustment.VisitId), Builders<BsonDocument>.Update.Set("tope", adjustment.NewAmount).Set(_updatedField, nowUtc), cancellationToken: token);
            }

            await Quotes.UpdateOneAsync(transaction, filter.Eq("_id", adjustment.QuoteId), quoteUpdate, cancellationToken: token);
            await outbox.AddAsync(transaction, OutboxMessage.Create(eventType, adjustment.Id.ToString(), new BsonDocument { { "ajusteId", adjustment.Id.ToString() }, { "cotizacionId", adjustment.QuoteId.ToString() }, { "visitaId", adjustment.VisitId.ToString() }, { "decision", newState } }, nowUtc), token);
            return true;
        }, false, cancellationToken);

    private static BsonDocument AdjustmentIdentifiers(ObjectId id, ObjectId quoteId, VisitRecord visit) => new()
    {
        { "ajusteId", id.ToString() },
        { "cotizacionId", quoteId.ToString() },
        { "visitaId", visit.Id.ToString() },
        { "clienteId", visit.ClientId },
        { "tecnicoUsuarioId", visit.TechnicianUserId },
    };
}
