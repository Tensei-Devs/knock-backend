using System.Text.RegularExpressions;

namespace Knock.Market.Api.Modules.Catalog.Domain;

/// <summary>Códigos de negocio del catálogo de oficios (spec 045 §5): en inglés y en <c>snake_case</c>.</summary>
internal static class TradeErrorCodes
{
    public const string TradeNotFoundCode = "trade_not_found";
    public const string TradeKeyTakenCode = "trade_key_taken";

    /// <summary>404 uniforme de «Otro oficio» para quien no es técnico en registro: no revela que el recurso existe (CA-22).</summary>
    public const string NotFoundCode = "not_found";

    /// <summary>La auditoría del panel no está disponible: la alta o el cambio no se hace (R-20).</summary>
    public const string AuditUnavailableCode = "audit_unavailable";
}

/// <summary>La auditoría del panel no está disponible: la alta o el cambio del catálogo no se hace (spec 045, R-20). El controlador la traduce a 503 <c>audit_unavailable</c>.</summary>
public sealed class PanelAuditUnavailableException : Exception
{
    public PanelAuditUnavailableException()
        : base("The panel audit trail is not available.")
    {
    }

    public PanelAuditUnavailableException(string message)
        : base(message)
    {
    }

    public PanelAuditUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Reglas puras del oficio que administra el panel y del oficio pedido (spec 045, R-18 a R-22): clave, nombre, modalidad, porcentajes, montos y el texto de «Otro oficio». Los valores salen de §6 y
/// de las decisiones D-04 a D-07 de la spec.
/// </summary>
internal static partial class TradeRules
{
    public const int MaxKeyLength = Oficio.MaxClaveLength;

    /// <summary>Máximo del nombre de un oficio nuevo o editado (§6): 40, más estricto que el de las semillas (100).</summary>
    public const int MaxNameLength = 40;

    public const int MaxSuggestedPhrases = 10;
    public const int MaxSuggestedPhraseLength = 60;
    public const int MinRequestTextLength = 2;
    public const int MaxRequestTextLength = 60;
    public const string DomicilioModality = "domicilio";
    public const string TallerModality = "taller";
    public const string BothModality = "ambos";

    /// <summary>Modalidades que acepta el panel (D-07): las de la atención del técnico y de la visita (spec 021).</summary>
    public static readonly IReadOnlyList<string> Modalities = [DomicilioModality, TallerModality, BothModality];

    /// <summary>Clave del oficio: minúsculas, dígitos, guion y guion bajo, de 1 a 16, sin espacios (R-18).</summary>
    public static bool IsValidKey(string? key) => key is not null && KeyPattern().IsMatch(key);

    public static bool IsValidName(string? name) =>
        name is not null && name.Trim().Length is >= 1 and <= MaxNameLength && !name.Any(char.IsControl);

    public static bool IsValidModality(string? modality) => modality is not null && Modalities.Contains(modality, StringComparer.Ordinal);

    /// <summary>Porcentaje entre 0 y 100 (R-18).</summary>
    public static bool IsValidPercentage(decimal? value) => value is >= 0 and <= 100;

    /// <summary>Monto o cantidad mayor que 0 (R-18).</summary>
    public static bool IsPositive(int? value) => value is > 0;

    public static bool IsValidPhrases(IReadOnlyList<string>? phrases) =>
        phrases is null
        || (phrases.Count <= MaxSuggestedPhrases
            && phrases.All(phrase => phrase is not null && phrase.Trim().Length is >= 1 and <= MaxSuggestedPhraseLength && !phrase.Any(char.IsControl)));

    /// <summary>
    /// Texto de «Otro oficio» (R-21): de 2 a 60 caracteres después de recortar, solo letras, dígitos y los signos de un nombre de oficio, y sin datos de contacto. La moderación es de patrones
    /// (correo, dirección web o más de seis dígitos seguidos); la de contenido con Azure es de la spec 031 y no está integrada.
    /// </summary>
    public static bool IsValidRequestText(string? text) =>
        text is not null
        && text.Trim().Length is >= MinRequestTextLength and <= MaxRequestTextLength
        && RequestTextPattern().IsMatch(text)
        && !ContactPattern().IsMatch(text);

    /// <summary>Forma normalizada (D-06): sin acentos, en minúsculas y con los espacios recortados y colapsados. «Plomero» y «plomero » dan lo mismo.</summary>
    public static string NormalizeRequestText(string text) => SearchNameNormalizer.NormalizeQuery(text);

    [GeneratedRegex(@"\A[a-z0-9_-]{1,16}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex KeyPattern();

    [GeneratedRegex(@"\A[\p{L}\p{N} .,'’()/&+-]+\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex RequestTextPattern();

    [GeneratedRegex(@"@|https?:|www\.|\d[\s.-]*\d[\s.-]*\d[\s.-]*\d[\s.-]*\d[\s.-]*\d[\s.-]*\d", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 200)]
    private static partial Regex ContactPattern();
}
