using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Moderation.Data;
using Knock.Identity.Api.Modules.Moderation.Domain;
using Knock.Identity.Api.Modules.Moderation.Dtos;
using Knock.Identity.Api.Modules.Panel.Data;
using Knock.Identity.Api.Modules.Panel.Services;
using MongoDB.Bson;

namespace Knock.Identity.Api.Modules.Moderation.Services;

/// <summary>
/// Cola y decisión de reportes (spec 053, REP-04 y REP-05). La cola muestra motivo y texto, nunca teléfono ni dirección (R-19). La decisión asigna la gravedad, deriva la respuesta (D-06), la aplica
/// con <see cref="IReportDecisionEffects"/> y deja la auditoría, en una sola transacción (R-22). Un moderador no puede decidir un reporte que lo involucra (spec 059, R-07).
/// </summary>
internal sealed class ReportReviewService(
    IReportReviewStore store,
    IReportDecisionEffects effects,
    AuditoriaPanel audit,
    TimeProvider clock)
{
    private static readonly string[] _pendingStates = ["abierto", "en_apelacion"];

    /// <summary>REP-04: por gravedad y antigüedad; por omisión los reportes pendientes de los últimos 30 días.</summary>
    public async Task<ReportQueuePage> ListAsync(ReportQueueQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var (from, to) = ReviewRules.Range(query.Desde?.UtcDateTime, query.Hasta?.UtcDateTime, clock.GetUtcNow().UtcDateTime);
        IReadOnlyList<string> states = query.Estado is null ? _pendingStates : [query.Estado];
        var rows = await store.ListAsync(query.Gravedad, states, from, to, ReviewCursor.Parse(query.Cursor), query.Limite + 1, cancellationToken);
        var page = rows.Take(query.Limite).ToList();
        return new ReportQueuePage(
            [.. page.Select(r => new ReportQueueItem(r.Id.ToString(), r.Reason, r.Text, r.Severity, r.State, r.CreatedAt))],
            rows.Count > query.Limite ? new ReviewCursor(page[^1].Rank, page[^1].CreatedAt, page[^1].Id).Encode() : null);
    }

    /// <summary>REP-05. 404 <c>report_not_found</c>, 409 <c>report_already_decided</c>, 403 <c>self_review_not_allowed</c>, 503 <c>audit_unavailable</c>.</summary>
    public async Task<ReportDecisionResult> DecideAsync(ReviewActor actor, string id, ReportDecisionBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(body);
        var objectId = ObjectId.Parse(id);
        var parties = await store.FindPartiesAsync(objectId, cancellationToken) ?? throw new DomainException(DomainErrorKind.NotFound, "The report does not exist.", ModerationErrorCodes.ReportNotFoundCode);
        if (string.Equals(actor.Sub, parties.ReportedId, StringComparison.OrdinalIgnoreCase) || string.Equals(actor.Sub, parties.ReporterId, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(DomainErrorKind.Forbidden, "A moderator cannot decide a report about themselves.", ModerationErrorCodes.SelfReviewCode);
        }

        var severity = body.Gravedad!;
        var response = ReportSeverities.ResponseFor(severity);
        var now = VerificationReviewService.Truncate(clock.GetUtcNow().UtcDateTime);
        var result = await store.DecideAsync(
            new ReportDecisionRecord(objectId, severity, response, body.Motivo!.Trim(), actor.Sub, now),
            (session, decided) => effects.ApplyAsync(session, new ReportDecisionApplied(id, severity, response, decided.ReporterId, decided.ReportedId, actor.Sub), cancellationToken),
            session => audit.WriteInTransactionAsync(session, PanelAuditValues.TipoAccion, ModerationAuditEvents.ReportDecided, severity, actor.Sub, id, cancellationToken),
            cancellationToken);
        return result.Outcome switch
        {
            DecisionStoreOutcome.Decided => new ReportDecisionResult(id, "decidido", severity, response),
            DecisionStoreOutcome.AlreadyDecided => throw new DomainException(DomainErrorKind.Conflict, "The report was already decided.", ModerationErrorCodes.ReportAlreadyDecidedCode),
            _ => throw new DomainException(DomainErrorKind.NotFound, "The report does not exist.", ModerationErrorCodes.ReportNotFoundCode),
        };
    }
}
