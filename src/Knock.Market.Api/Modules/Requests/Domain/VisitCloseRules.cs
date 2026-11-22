namespace Knock.Market.Api.Modules.Requests.Domain;

/// <summary>Reglas puras del cierre de la visita (spec 050, R-28 a R-30): textos, fotos y plazos de terminar, reportar que no se terminó y el permiso de fotos.</summary>
internal static class VisitCloseRules
{
    public const string Finished = "terminada";
    public const string FinishedEvent = "VisitaTerminada";
    public const string NotFinishedEvent = "ReporteNoTerminadoCreado";
    public const string PhotoPermissionEvent = "PermisoFotosCambiado";
    public const string CloseOpinionTask = "CerrarPeriodoDeOpinion";
    public const string CloseReportTask = "CerrarReporteDeNoTerminado";
    public const string UnderReview = "en_revision";

    public const int MinDescriptionLength = 8;
    public const int MaxDescriptionLength = 500;
    public const int MaxRecommendationLength = 300;
    public const int MinPhotos = 1;
    public const int MaxPhotos = 5;

    /// <summary>Identificador de una foto: el <c>archivoId</c> del módulo de archivos (ver <see cref="PhotoIds"/>).</summary>
    public static bool TryParsePhotoId(string? value, out Guid fileId) => PhotoIds.TryParse(value, out fileId);

    public const int MinReportLength = 8;
    public const int MaxReportLength = 500;

    public static readonly TimeSpan ReportWindow = TimeSpan.FromDays(7);
    public static readonly TimeSpan OpinionPeriod = TimeSpan.FromDays(30);

    /// <summary>La descripción de lo hecho mide de 8 a 500 caracteres sin contar espacios de los extremos (R-28).</summary>
    public static bool IsValidDescription(string? text) => text?.Trim().Length is >= MinDescriptionLength and <= MaxDescriptionLength;

    /// <summary>La recomendación es opcional y mide hasta 300 caracteres (R-28).</summary>
    public static bool IsValidRecommendation(string? text) => text is null || text.Trim().Length <= MaxRecommendationLength;

    /// <summary>El reporte de «no terminado» mide de 8 a 500 caracteres (R-29).</summary>
    public static bool IsValidReport(string? text) => text?.Trim().Length is >= MinReportLength and <= MaxReportLength;

    /// <summary>El cliente puede reportar solo dentro de los 7 días siguientes a terminada la visita (R-29).</summary>
    public static bool IsInReportWindow(DateTime nowUtc, DateTime finishedAtUtc) => nowUtc >= finishedAtUtc && nowUtc - finishedAtUtc <= ReportWindow;
}
