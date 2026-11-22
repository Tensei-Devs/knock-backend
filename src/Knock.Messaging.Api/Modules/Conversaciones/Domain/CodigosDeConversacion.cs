namespace Knock.Messaging.Api.Modules.Conversaciones.Domain;

/// <summary>
/// <c>code</c> estable de los errores del módulo (spec 030, §5): en inglés y en <c>snake_case</c>, sin datos de la persona ni del recurso. Se declaran como constantes <c>...Code</c> para que
/// la prueba de convención de errores (spec 002, CA-07) los vea.
/// </summary>
internal static class CodigosDeConversacion
{
    public const string NotFoundCode = "not_found";
    public const string ConversationClosedCode = "conversation_closed";
    public const string ContentRejectedCode = "content_rejected";
    public const string ModerationUnavailableCode = "moderation_unavailable";
    public const string MarketUnavailableCode = "market_unavailable";
    public const string MessageLimitCode = "message_limit";

    /// <summary>Motivo con el que se cierra una conversación por un bloqueo (spec 043, R-18): el mismo de un técnico descartado, para no delatar el bloqueo (R-17).</summary>
    public const string BlockClosureReason = "dismissed";
}
