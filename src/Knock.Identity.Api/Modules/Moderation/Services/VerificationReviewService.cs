using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Moderation.Data;
using Knock.Identity.Api.Modules.Moderation.Domain;
using Knock.Identity.Api.Modules.Moderation.Dtos;
using Knock.Identity.Api.Modules.Panel;
using Knock.Identity.Api.Modules.Trust;
using MongoDB.Bson;

namespace Knock.Identity.Api.Modules.Moderation.Services;

/// <summary>Quien actúa: siempre el <c>sub</c> y el <c>sid</c> del token del panel (spec 053, R-23); ningún cuerpo ni ruta los reemplaza.</summary>
internal sealed record ReviewActor(string Sub, string? Sid);

/// <summary>
/// Cola, apertura y decisión de verificaciones y recuperaciones (spec 053, VER-03 a VER-05). SEGURIDAD: maneja fotos de identidad. Abrir escribe primero la auditoría y, si falla, no entrega
/// enlaces (R-17, R-22); los enlaces no se guardan ni se registran; decidir destruye la llave y deja el registro de auditoría en la misma transacción; quien decide nunca es la persona
/// verificada (D-05). No toca MongoDB: eso es de los almacenes.
/// </summary>
internal sealed partial class VerificationReviewService(
    IVerificationReviewStore store,
    IAccessRecoveryReviews recoveries,
    IVerificationPhotoLinks links,
    IVerificationBlobPurger blobs,
    IPanelApi panel,
    ITrustApi trust,
    TimeProvider clock,
    ILogger<VerificationReviewService> logger)
{
    /// <summary>Los enlaces de las fotos duran 5 minutos (R-17, D-04).</summary>
    public static readonly TimeSpan PhotoLinkLifetime = TimeSpan.FromMinutes(5);

    /// <summary>VER-03: la cola, de la más antigua a la más reciente, con el estado <c>en_revision</c> por omisión y los últimos 30 días (R-16).</summary>
    public async Task<VerificationQueuePage> ListAsync(VerificationQueueQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var (from, to) = ReviewRules.Range(query.Desde?.UtcDateTime, query.Hasta?.UtcDateTime, clock.GetUtcNow().UtcDateTime);
        var kind = query.Tipo ?? ReviewKinds.Identity;
        var state = query.Estado ?? ReviewStates.UnderReview;
        var after = ReviewCursor.Parse(query.Cursor);
        var rows = kind == ReviewKinds.Recovery
            ? await recoveries.ListAsync(state, from, to, after, query.Limite + 1, cancellationToken)
            : await store.ListAsync(state, from, to, after, query.Limite + 1, cancellationToken);
        var page = rows.Take(query.Limite).ToList();
        return new VerificationQueuePage(
            [.. page.Select(r => new VerificationQueueItem(r.Id.ToString(), kind, r.Level, r.State, r.SentAt))],
            rows.Count > query.Limite ? new ReviewCursor(0, page[^1].SentAt, page[^1].Id).Encode() : null);
    }

    /// <summary>VER-04: abre una verificación, registra quién la abrió y entrega los enlaces de 5 minutos. 404 si no existe, 410 si las fotos ya se destruyeron.</summary>
    public async Task<VerificationDetail> OpenAsync(ReviewActor actor, string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var verification = await FindOrThrowAsync(id, cancellationToken);
        EnsureNotSelf(actor, verification);
        if (verification.PhotosDestroyed || verification.State != ReviewStates.UnderReview)
        {
            throw new DomainException(DomainErrorKind.Gone, "The photos were already destroyed.", ModerationErrorCodes.PhotosDestroyedCode);
        }

        // La auditoría va antes que los enlaces: si no se puede registrar quién los abrió, nadie los recibe (R-17 y R-22).
        await panel.RecordActionAsync(ModerationAuditEvents.VerificationOpened, PanelApiAuditValues.ResultAllowed, actor.Sub, actor.Sid, PanelApiPermissions.VerificationReview, id, cancellationToken);
        var issued = await links.IssueAsync(id, verification.PhotoTypes, PhotoLinkLifetime, cancellationToken);
        return new VerificationDetail(id, ReviewKinds.Identity, verification.Level, verification.State, verification.SentAt, issued);
    }

    /// <summary>
    /// VER-05: aprueba o rechaza. Una verificación de identidad destruye la llave de las fotos en la misma escritura; una recuperación de acceso cambia el número (spec 039). 404 si no existe,
    /// 409 <c>verification_already_decided</c> si ya no está en revisión, 403 si quien decide es la persona verificada, 503 <c>audit_unavailable</c> si no se puede auditar.
    /// </summary>
    public async Task<VerificationDecisionResult> DecideAsync(ReviewActor actor, string id, VerificationDecisionBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(body);
        var objectId = ObjectId.Parse(id);
        var approved = body.Decision == ReviewDecisions.Approved;
        var now = Truncate(clock.GetUtcNow().UtcDateTime);
        var result = approved ? ReviewDecisions.Approved : ReviewDecisions.Rejected;
        var verification = await store.FindAsync(objectId, cancellationToken);
        if (verification is null)
        {
            return await DecideRecoveryAsync(actor, objectId, approved, result, now, cancellationToken);
        }

        EnsureNotSelf(actor, verification);
        var note = (body.Motivo ?? string.Empty).Trim();
        var catalogReason = !approved && trust.IsCatalogReason(note) ? note : null;
        var outcome = await store.DecideAsync(
            new VerificationDecisionRecord(objectId, approved, actor.Sub, note, catalogReason, now),
            session => panel.RecordActionInTransactionAsync(session, ModerationAuditEvents.VerificationDecided, result, actor.Sub, id, cancellationToken),
            cancellationToken);
        switch (outcome)
        {
            case DecisionStoreOutcome.NotFound:
                throw NotFound();
            case DecisionStoreOutcome.AlreadyDecided:
                throw AlreadyDecided();
            case DecisionStoreOutcome.CurpDuplicate:
                throw new DomainException(DomainErrorKind.Conflict, "The CURP already belongs to a verified account.", VerificationErrors.CurpDuplicateCode);
        }

        await DeleteBlobsAsync(id, cancellationToken);
        return new VerificationDecisionResult(id, approved ? ReviewStates.Verified : ReviewStates.Rejected, now);
    }

    private async Task<VerificationDecisionResult> DecideRecoveryAsync(ReviewActor actor, ObjectId id, bool approved, string result, DateTime now, CancellationToken cancellationToken)
    {
        var outcome = await recoveries.DecideAsync(
            id,
            approved,
            actor.Sub,
            session => panel.RecordActionInTransactionAsync(session, ModerationAuditEvents.RecoveryDecided, result, actor.Sub, id.ToString(), cancellationToken),
            cancellationToken);
        return outcome switch
        {
            RecoveryDecisionOutcome.Decided => new VerificationDecisionResult(id.ToString(), approved ? ReviewStates.Verified : ReviewStates.Rejected, now),
            RecoveryDecisionOutcome.AlreadyDecided => throw AlreadyDecided(),
            _ => throw NotFound(),
        };
    }

    /// <summary>Después de confirmar: borrar los blobs es de la spec 057 y no revierte la decisión (la llave ya no existe, las fotos son ilegibles). El fallo se registra sin datos y la tarea lo reintenta.</summary>
    private async Task DeleteBlobsAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            await blobs.PurgeBlobsAsync(id, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogBlobCleanupFailed(logger, exception);
        }
    }

    private async Task<ReviewedVerification> FindOrThrowAsync(string id, CancellationToken cancellationToken)
    {
        var verification = await store.FindAsync(ObjectId.Parse(id), cancellationToken);
        return verification is null || verification.State == ReviewStates.Capturing ? throw NotFound() : verification;
    }

    /// <summary>D-05: quien decide, o abre las fotos, no puede ser la persona verificada.</summary>
    private static void EnsureNotSelf(ReviewActor actor, ReviewedVerification verification)
    {
        if (string.Equals(actor.Sub, verification.OwnerSub, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(DomainErrorKind.Forbidden, "A person cannot review their own verification.", ModerationErrorCodes.SelfReviewCode);
        }
    }

    private static DomainException NotFound() => new(DomainErrorKind.NotFound, "The verification does not exist.", ModerationErrorCodes.VerificationNotFoundCode);

    private static DomainException AlreadyDecided() => new(DomainErrorKind.Conflict, "The verification is not under review.", ModerationErrorCodes.AlreadyDecidedCode);

    [LoggerMessage(EventId = 3401, Level = LogLevel.Warning, Message = "Verification blob cleanup failed after the decision; the retry task will finish it")]
    private static partial void LogBlobCleanupFailed(ILogger logger, Exception exception);

    /// <summary>MongoDB guarda milisegundos: se trunca para que la respuesta coincida con lo guardado.</summary>
    internal static DateTime Truncate(DateTime value) => value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMillisecond));
}
