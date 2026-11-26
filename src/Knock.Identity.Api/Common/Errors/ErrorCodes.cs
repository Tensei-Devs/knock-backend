namespace Knock.Identity.Api.Common.Errors;

/// <summary>Códigos de error de dominio que usan varios módulos: viven aquí para que un módulo no dependa de los servicios de otro.</summary>
internal static class CommonErrorCodes
{
    /// <summary>Mercado no respondió; sin poder validar la colonia no se guarda nada.</summary>
    public const string MarketUnavailable = "market_unavailable";

    /// <summary>La cuenta tiene una eliminación programada: no se cambia el perfil (spec 017) ni se crea una verificación (spec 025, R-20).</summary>
    public const string DeletionScheduled = "deletion_scheduled";

    /// <summary>Otra cuenta ya tiene esa CURP en revisión o verificada (409): Trust al crear y Moderation al aprobar devuelven el mismo código.</summary>
    public const string CurpDuplicate = "curp_duplicate";

    /// <summary>Respuesta uniforme «no existe» (404) que no distingue el motivo: direcciones (spec 040) y apelación de una persona suspendida (spec 060, R-02).</summary>
    public const string NotFound = "not_found";

    /// <summary>La persona que revisa es parte del caso (verificación, reporte o apelación): 403.</summary>
    public const string SelfReview = "self_review_not_allowed";
}
