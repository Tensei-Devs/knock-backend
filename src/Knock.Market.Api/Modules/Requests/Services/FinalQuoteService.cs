using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Common.TareasProgramadas;
using Knock.Market.Api.Modules.Requests.Data;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;
using MongoDB.Bson;

namespace Knock.Market.Api.Modules.Requests.Services;

/// <summary>Por qué una operación de la spec 049 no se aplicó, o <see cref="Done"/>. Cada caso es una respuesta fija del controller.</summary>
internal enum FinalQuoteStatus
{
    Done,
    NotATechnician,
    NotTheOwner,
    NotFound,
    NotAllowed,
    AlreadyDecided,
    AmountTooLow,
    ContentRejected,
    ModerationUnavailable,
}

internal sealed record FinalQuoteOutcome<T>(FinalQuoteStatus Status, T? Value = default)
    where T : class;

/// <summary>
/// Cotización final tras la visita y ajustes de precio (spec 049, HU sin tarjeta propia): resuelve el papel de la persona por el <c>sub</c> del token (R-18), aplica las reglas de
/// estado y delega las escrituras atómicas en <see cref="FinalQuoteStore"/>. No toca MongoDB y no registra precios ni textos de las personas (R-19).
/// </summary>
internal sealed class FinalQuoteService(
    FinalQuoteStore store,
    VisitStore visits,
    ITechnicianDirectory technicians,
    INoteModerator moderator,
    TimeProvider clock)
{
    /// <summary>COT-06: el técnico de la visita en curso envía la cotización final de una visita previa (una sola por visita).</summary>
    public async Task<FinalQuoteOutcome<FinalQuoteResponse>> SendFinalQuoteAsync(string sub, string id, int price, string description, CancellationToken cancellationToken)
    {
        if (!await technicians.ExistsAsync(sub, cancellationToken))
        {
            return new(FinalQuoteStatus.NotATechnician);
        }

        var visit = await FindVisitAsync(id, cancellationToken);
        if (visit is null || VisitAccess.RoleOf(sub, visit.ClientId, visit.TechnicianUserId) != VisitRole.Technician)
        {
            return new(FinalQuoteStatus.NotFound);
        }

        if (visit.Estado != VisitRules.InProgress || await store.QuoteTypeAsync(visit.QuoteId, cancellationToken) != QuoteComparer.VisitType)
        {
            return new(FinalQuoteStatus.NotAllowed);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        return await store.CreateFinalQuoteAsync(visit, price, description.Trim(), now, cancellationToken) is { } expires
            ? new(FinalQuoteStatus.Done, new FinalQuoteResponse(visit.Id.ToString(), price, FinalQuoteRules.Sent, new DateTimeOffset(expires, TimeSpan.Zero)))
            : new(FinalQuoteStatus.NotAllowed);
    }

    /// <summary>COT-07: el cliente dueño aprueba o rechaza la cotización final pendiente. Al rechazar, la visita queda terminada con <c>solo_visita</c>.</summary>
    public async Task<FinalQuoteOutcome<FinalQuoteDecisionResponse>> DecideFinalQuoteAsync(string sub, string id, string decision, CancellationToken cancellationToken)
    {
        var visit = await FindVisitAsync(id, cancellationToken);
        var role = visit is null ? VisitRole.Stranger : VisitAccess.RoleOf(sub, visit.ClientId, visit.TechnicianUserId);
        if (visit is null || role == VisitRole.Stranger)
        {
            return new(FinalQuoteStatus.NotFound);
        }

        if (role != VisitRole.Client)
        {
            return new(FinalQuoteStatus.NotTheOwner);
        }

        var approve = decision == FinalQuoteRules.Approve;
        if (!await store.DecideFinalQuoteAsync(visit, approve, clock.GetUtcNow().UtcDateTime, cancellationToken))
        {
            return new(FinalQuoteStatus.AlreadyDecided);
        }

        return new(FinalQuoteStatus.Done, new FinalQuoteDecisionResponse(visit.Id.ToString(), approve ? FinalQuoteRules.Approved : FinalQuoteRules.Rejected, approve ? null : FinalQuoteRules.OnlyVisitResult));
    }

    /// <summary>AJU-01: el técnico de una cotización aceptada propone un monto mayor al tope, con motivo moderado, mientras la visita está en curso.</summary>
    public async Task<FinalQuoteOutcome<AdjustmentResponse>> ProposeAdjustmentAsync(string sub, string id, int newAmount, string reason, CancellationToken cancellationToken)
    {
        if (!await technicians.ExistsAsync(sub, cancellationToken))
        {
            return new(FinalQuoteStatus.NotATechnician);
        }

        if (!ObjectId.TryParse(id, out var quoteId))
        {
            throw new DomainException(DomainErrorKind.BadRequest, "The quote id is not valid.");
        }

        var quote = await store.FindQuoteAsync(quoteId, cancellationToken);
        if (quote is null || !string.Equals(quote.TechnicianUserId, sub, StringComparison.Ordinal))
        {
            return new(FinalQuoteStatus.NotFound);
        }

        var visitId = quote.State == "aceptada" && !quote.HasPendingAdjustment ? await store.FindVisitIdByQuoteAsync(quoteId, cancellationToken) : null;
        var visit = visitId is { } found ? await visits.FindAsync(found, cancellationToken) : null;
        if (visit is null || visit.Estado != VisitRules.InProgress)
        {
            return new(FinalQuoteStatus.NotAllowed);
        }

        if (newAmount < FinalQuoteRules.MinimumAdjustedAmount(quote.Cap))
        {
            return new(FinalQuoteStatus.AmountTooLow);
        }

        var moderation = await moderator.ModerateAsync(reason, cancellationToken);
        if (moderation != NoteModerationResult.Approved)
        {
            return new(moderation == NoteModerationResult.Unavailable ? FinalQuoteStatus.ModerationUnavailable : FinalQuoteStatus.ContentRejected);
        }

        return await store.CreateAdjustmentAsync(quote, visit, newAmount, reason.Trim(), clock.GetUtcNow().UtcDateTime, cancellationToken) is { } created
            ? new(FinalQuoteStatus.Done, new AdjustmentResponse(created.Id.ToString(), quote.Id.ToString(), visit.Id.ToString(), newAmount, FinalQuoteRules.Pending, new DateTimeOffset(created.ExpiresAtUtc, TimeSpan.Zero)))
            : new(FinalQuoteStatus.NotAllowed);
    }

    /// <summary>AJU-02: el cliente dueño aprueba o rechaza el ajuste pendiente; el precio solo cambia si aprueba.</summary>
    public async Task<FinalQuoteOutcome<AdjustmentDecisionResponse>> DecideAdjustmentAsync(string sub, string id, string decision, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var adjustmentId))
        {
            throw new DomainException(DomainErrorKind.BadRequest, "The adjustment id is not valid.");
        }

        var adjustment = await store.FindAdjustmentAsync(adjustmentId, cancellationToken);
        var role = adjustment is null ? VisitRole.Stranger : VisitAccess.RoleOf(sub, adjustment.ClientId, adjustment.TechnicianUserId);
        if (adjustment is null || role == VisitRole.Stranger)
        {
            return new(FinalQuoteStatus.NotFound);
        }

        if (role != VisitRole.Client)
        {
            return new(FinalQuoteStatus.NotTheOwner);
        }

        var approve = decision == FinalQuoteRules.Approve;
        if (!await store.DecideAdjustmentAsync(adjustment, approve, clock.GetUtcNow().UtcDateTime, cancellationToken))
        {
            return new(FinalQuoteStatus.AlreadyDecided);
        }

        return new(FinalQuoteStatus.Done, new AdjustmentDecisionResponse(adjustment.Id.ToString(), approve ? FinalQuoteRules.AdjustmentApproved : FinalQuoteRules.AdjustmentRejected, approve ? adjustment.NewAmount : null));
    }

    private async Task<VisitRecord?> FindVisitAsync(string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var visitId))
        {
            throw new DomainException(DomainErrorKind.BadRequest, "The visit id is not valid.");
        }

        return await visits.FindAsync(visitId, cancellationToken);
    }
}

/// <summary>Base de las tareas de vencimiento de la spec 049: lee la clave de la tarea y vence la entidad con la hora del reloj.</summary>
internal abstract class FinalQuoteExpiryTask(TimeProvider clock) : IManejadorDeTarea
{
    public abstract string Tipo { get; }

    public async Task HandleAsync(TareaReservada task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (!ObjectId.TryParse(task.ClaveNegocio, out var id))
        {
            throw new FormatException("Invalid expiry task key.");
        }

        await ExpireAsync(id, clock.GetUtcNow().UtcDateTime, cancellationToken);
    }

    protected abstract Task ExpireAsync(ObjectId id, DateTime nowUtc, CancellationToken cancellationToken);
}

/// <summary>Tarea <c>VencerCotizacionFinal</c> (spec 054): la cotización final sin respuesta a las 48 horas queda vencida y la visita termina como <c>solo_visita</c>.</summary>
internal sealed class VencerCotizacionFinal(FinalQuoteStore store, TimeProvider clock) : FinalQuoteExpiryTask(clock)
{
    public override string Tipo => FinalQuoteRules.FinalQuoteExpiryTask;

    protected override Task ExpireAsync(ObjectId id, DateTime nowUtc, CancellationToken cancellationToken) => store.ExpireFinalQuoteAsync(id, nowUtc, cancellationToken);
}

/// <summary>Tarea <c>VencerAjuste</c> (spec 054): el ajuste sin respuesta a las 24 horas queda vencido y el precio sigue siendo el tope aprobado.</summary>
internal sealed class VencerAjuste(FinalQuoteStore store, TimeProvider clock) : FinalQuoteExpiryTask(clock)
{
    public override string Tipo => FinalQuoteRules.AdjustmentExpiryTask;

    protected override Task ExpireAsync(ObjectId id, DateTime nowUtc, CancellationToken cancellationToken) => store.ExpireAdjustmentAsync(id, nowUtc, cancellationToken);
}
