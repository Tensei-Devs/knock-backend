namespace Knock.Market.Api.Modules.Reviews.Domain;

/// <summary>Reglas puras de las opiniones y la reputación del técnico (spec 051, R-12 a R-17): plazos, límites, estados de moderación y niveles.</summary>
internal static class ReviewRules
{
    public const string ReviewNotAllowedCode = "review_not_allowed";
    public const string ReviewAlreadyAnsweredCode = "review_already_answered";
    public const string ReviewNotFoundCode = "review_not_found";
    public const string VisitNotFoundCode = "visit_not_found";
    public const string TechnicianNotFound = Tecnicos.Domain.TechnicianRules.TechnicianNotFoundCode;
    public const string ForbiddenCode = "forbidden";

    public const string CreatedEvent = "OpinionCreada";
    public const string AnsweredEvent = "OpinionRespondida";
    public const string PeriodClosedEvent = "PeriodoDeOpinionCerrado";
    public const string CloseOpinionTask = "CerrarPeriodoDeOpinion";

    /// <summary>Estado de moderación del comentario: sin comentario o aprobado se publica; pendiente o rechazado no sale en la lista pública (R-17, D-04).</summary>
    public const string Published = "publicada";
    public const string Pending = "pendiente";
    public const string Rejected = "rechazada";

    public const string NewLevel = "nuevo";
    public const string TrustedLevel = "confiable";
    public const string OutstandingLevel = "destacado";

    public const int MaxText = 500;
    public const int MaxPhotos = 3;
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;

    private const int _trustedMinCount = 5;
    private const double _trustedMinAverage = 4.0;
    private const int _outstandingMinCount = 20;
    private const double _outstandingMinAverage = 4.7;

    public static readonly TimeSpan OpinionPeriod = TimeSpan.FromDays(30);

    public static bool IsValidStars(int? value) => value is >= 1 and <= 5;

    public static bool IsValidText(string? text) => text is null || (text.Trim().Length <= MaxText && !text.Any(char.IsControl));

    /// <summary>La respuesta del técnico es obligatoria y mide de 1 a 500 caracteres (R-15).</summary>
    public static bool IsValidAnswer(string? text) => text is not null && text.Trim().Length is >= 1 and <= MaxText && !text.Any(char.IsControl);

    /// <summary>La visita se puede calificar solo terminada y dentro de los 30 días siguientes (R-12).</summary>
    public static bool IsWithinPeriod(DateTime nowUtc, DateTime finishedAtUtc) => nowUtc >= finishedAtUtc && nowUtc - finishedAtUtc <= OpinionPeriod;

    /// <summary>Nivel de la reputación (R-14): <c>nuevo</c> con menos de 5 opiniones, <c>confiable</c> con 5 y promedio de 4.0 y <c>destacado</c> con 20 y 4.7.</summary>
    public static string LevelOf(int total, double average) => total switch
    {
        >= _outstandingMinCount when average >= _outstandingMinAverage => OutstandingLevel,
        >= _trustedMinCount when average >= _trustedMinAverage => TrustedLevel,
        _ => NewLevel,
    };

    /// <summary>Promedio con un decimal, o <see langword="null"/> sin opiniones.</summary>
    public static double? AverageOf(IReadOnlyDictionary<int, int> distribution)
    {
        ArgumentNullException.ThrowIfNull(distribution);
        var total = distribution.Values.Sum();
        return total == 0 ? null : Math.Round(distribution.Sum(x => (double)x.Key * x.Value) / total, 1, MidpointRounding.AwayFromZero);
    }
}
