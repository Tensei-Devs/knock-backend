namespace Knock.Market.Api.Modules.Requests.Domain;

/// <summary><c>code</c> de negocio de la cotización final y los ajustes de precio (spec 049, §5): en inglés y en <c>snake_case</c>, sin datos de la persona.</summary>
internal static class FinalQuoteErrorCodes
{
    public const string FinalQuoteNotAllowedCode = "final_quote_not_allowed";
    public const string FinalQuoteAlreadyDecidedCode = "final_quote_already_decided";
    public const string AdjustmentNotAllowedCode = "adjustment_not_allowed";
    public const string AdjustmentAlreadyDecidedCode = "adjustment_already_decided";
}

/// <summary>Reglas puras de la cotización final y del ajuste de precio (spec 049, R-13 a R-16): plazos, rangos de texto y monto mínimo del ajuste.</summary>
internal static class FinalQuoteRules
{
    public const string Approve = "aprueba";
    public const string Reject = "rechaza";

    // Estados de la cotización final embebida en la visita.
    public const string Sent = "enviada";
    public const string Approved = "aprobada";
    public const string Rejected = "rechazada";
    public const string Expired = "vencida";

    // Estados del ajuste de precio.
    public const string Pending = "pendiente";
    public const string AdjustmentApproved = "aprobado";
    public const string AdjustmentRejected = "rechazado";
    public const string AdjustmentExpired = "vencido";

    public const string OnlyVisitResult = "solo_visita";
    public const string FinalQuoteExpiryTask = "VencerCotizacionFinal";
    public const string AdjustmentExpiryTask = "VencerAjuste";

    public const int MinPrice = 1;
    public const int MaxPrice = 2_000_000;
    public const int MinDescription = 8;
    public const int MaxDescription = 500;

    /// <summary>Largo mínimo del motivo: 8 caracteres (R-15). R-08 y §6 de la spec 049 aún dicen 10.</summary>
    public const int MinReason = 8;
    public const int MaxReason = 300;
    public const int MinRaise = 100;

    public static readonly TimeSpan FinalQuoteValidity = TimeSpan.FromHours(48);
    public static readonly TimeSpan AdjustmentValidity = TimeSpan.FromHours(24);

    public static bool IsDecision(string? value) => value is Approve or Reject;

    /// <summary>Monto mínimo que se puede proponer sobre un tope: al menos 100 pesos más (R-08). Nunca desborda.</summary>
    public static long MinimumAdjustedAmount(int cap) => (long)cap + MinRaise;

    public static DateTime FinalQuoteExpiresAt(DateTime nowUtc) => nowUtc + FinalQuoteValidity;

    public static DateTime AdjustmentExpiresAt(DateTime nowUtc) => nowUtc + AdjustmentValidity;
}
