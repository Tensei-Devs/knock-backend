using Knock.Contracts.Moderation;

namespace Knock.Market.Api.Common.Moderation;

/// <summary>Plantillas locales versionadas en español de México (spec 031, R-14). Sin revelar la categoría, el término ni la severidad. Revisión humana: son texto que ve la persona.</summary>
internal static class PlantillasDeModeracion
{
    public const string Version = "1";
    public const string ContentRejected = "content_rejected";
    public const string ImageRejected = "image_rejected";
    public const string ModerationUnavailable = "moderation_unavailable";
    public const string ModerationLimit = "moderation_limit";

    public static string Detail(string code) => code switch
    {
        ContentRejected => "Tu texto no cumple las reglas de la comunidad de Knock. Revísalo y vuelve a intentarlo.",
        ImageRejected => "Esta foto no se puede publicar. Elige otra que muestre solo el trabajo.",
        ModerationUnavailable => "No pudimos revisar tu contenido en este momento. Intenta de nuevo en unos minutos.",
        ModerationLimit => "Llegaste al límite de publicaciones de hoy. Intenta mañana.",
        _ => throw new ArgumentOutOfRangeException(nameof(code)),
    };
}

/// <summary>Cómo traduce un llamador el resultado a HTTP: estado, código de negocio, texto de la plantilla y <c>Retry-After</c> en segundos.</summary>
internal sealed record ModerationProblem(int Status, string Code, string Detail, int? RetryAfterSeconds);

/// <summary>Traductor único de <see cref="ModerationVerdict"/> a HTTP (spec 031, R-13), igual en todos los llamadores. <c>null</c> significa Approved: se guarda o se publica.</summary>
internal static class ModerationProblemMapper
{
    public const int UnavailableRetryAfterSeconds = 30;

    public static ModerationProblem? Map(ModerationVerdict verdict, bool isImage, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(verdict);
        return verdict.Decision switch
        {
            ModerationDecision.Approved => null,
            ModerationDecision.Rejected => Build(422, isImage ? PlantillasDeModeracion.ImageRejected : PlantillasDeModeracion.ContentRejected, null),
            _ when verdict.ReasonCode == ModerationReasonCodes.PerPersonLimit => Build(429, PlantillasDeModeracion.ModerationLimit, SecondsUntilMidnight(now)),
            _ => Build(503, PlantillasDeModeracion.ModerationUnavailable, UnavailableRetryAfterSeconds),
        };
    }

    /// <summary>Segundos hasta la medianoche de <c>America/Mexico_City</c> (mínimo 1).</summary>
    internal static int SecondsUntilMidnight(DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, ContentSafetyModerator.MexicoCity);
        var midnight = new DateTimeOffset(local.Date.AddDays(1), ContentSafetyModerator.MexicoCity.GetUtcOffset(local.Date.AddDays(1)));
        return Math.Max(1, (int)Math.Ceiling((midnight - now).TotalSeconds));
    }

    private static ModerationProblem Build(int status, string code, int? retryAfter) => new(status, code, PlantillasDeModeracion.Detail(code), retryAfter);
}
