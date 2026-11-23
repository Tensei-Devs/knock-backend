namespace Knock.Identity.Api.Modules.Safety.Domain;

/// <summary>Códigos de error de negocio de los bloqueos (spec 043, §5). En inglés y <c>snake_case</c>, sin datos de nadie.</summary>
internal static class SafetyErrorCodes
{
    public const string UserNotFoundCode = "user_not_found";
    public const string AlreadyBlockedCode = "already_blocked";
    public const string BlockNotFoundCode = "block_not_found";
    public const string InvalidCursorCode = "invalid_safety_cursor";
    public const string ContentRejectedCode = "report_content_rejected";
    public const string ModerationUnavailableCode = "report_moderation_unavailable";
    public const string AppealNotAllowedCode = "appeal_not_allowed";
    public const string CannotSuspendSelfCode = "cannot_suspend_self";
    public const string AccountNotSuspendedCode = "account_not_suspended";
    public const string NotFoundCode = "not_found";
    public const string AppealAlreadyResolvedCode = "appeal_already_resolved";
    public const string AppealNotFoundCode = "appeal_not_found";
    public const string SelfReviewCode = "self_review_not_allowed";
}
