using System.Globalization;
using System.Text;
using Knock.Identity.Api.Common.Errors;
using MongoDB.Bson;

namespace Knock.Identity.Api.Modules.Moderation.Domain;

/// <summary>Tipos de caso de la cola de verificaciones (spec 053, R-16): identidad (spec 025) y recuperación de acceso (spec 039).</summary>
internal static class ReviewKinds
{
    public const string Identity = "identidad";
    public const string Recovery = "recuperacion";

    public static bool IsKnown(string? kind) => kind is Identity or Recovery;
}

/// <summary>Decisiones posibles sobre una verificación (spec 053, R-18).</summary>
internal static class ReviewDecisions
{
    public const string Approved = "aprobada";
    public const string Rejected = "rechazada";

    public static bool IsKnown(string? decision) => decision is Approved or Rejected;
}

/// <summary>Gravedades de un reporte (spec 053, R-20) y la respuesta que se deriva de cada una (decisión D-06: no hay lista aparte de sanciones).</summary>
internal static class ReportSeverities
{
    public const string Critical = "critica";
    public const string High = "alta";
    public const string Medium = "media";
    public const string Low = "baja";

    /// <summary>De la más grave a la menos grave: es el orden de la cola.</summary>
    public static IReadOnlyList<string> All { get; } = [Critical, High, Medium, Low];

    public static bool IsKnown(string? severity) => severity is not null && All.Contains(severity, StringComparer.Ordinal);

    /// <summary>Posición en la cola: 0 la más grave; un reporte sin gravedad va al final de su grupo (4).</summary>
    public static int Rank(string? severity)
    {
        for (var index = 0; index < All.Count; index++)
        {
            if (string.Equals(All[index], severity, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return All.Count;
    }

    /// <summary>La respuesta de la gravedad (spec 053, R-20): suspensión preventiva, ocultar al técnico, advertencia o mediación.</summary>
    public static string ResponseFor(string severity) => severity switch
    {
        Critical => ReportResponses.PreventiveSuspension,
        High => ReportResponses.Hide,
        Medium => ReportResponses.Warning,
        _ => ReportResponses.Mediation,
    };
}

/// <summary>
/// Prioridad con la que se atiende un reporte decidido (spec 053, R-20, enmienda 2026-11-23). Se deriva aparte y NUNCA reemplaza la gravedad guardada: un reporte <c>critica</c> contra una persona que no es
/// técnica (no se suspende) sigue siendo <c>critica</c> y se atiende con prioridad <c>alta</c>; en los demás casos es <c>normal</c>.
/// </summary>
internal static class ReportPriorities
{
    public const string High = "alta";
    public const string Normal = "normal";

    public static string For(string severity, bool reportedIsClient) =>
        reportedIsClient && string.Equals(severity, ReportSeverities.Critical, StringComparison.Ordinal) ? High : Normal;
}

/// <summary>Valores de <c>decision.respuesta</c> de un reporte decidido.</summary>
internal static class ReportResponses
{
    public const string PreventiveSuspension = "suspension_preventiva";
    public const string Hide = "ocultar";
    public const string Warning = "advertencia";
    public const string Mediation = "mediacion";
}

/// <summary>Estados de la cola y valores guardados que esta spec lee (specs 025 y 043).</summary>
internal static class ReviewStates
{
    public const string UnderReview = "en_revision";
    public const string Verified = "verificada";
    public const string Rejected = "no_verificada";
    public const string NotVerified = "sin_verificar";
    public const string Capturing = "en_captura";

    public static readonly IReadOnlyList<string> Verification = [NotVerified, Capturing, UnderReview, Verified, Rejected];

    public static bool IsVerificationState(string? state) => state is not null && Verification.Contains(state, StringComparer.Ordinal);

    public static readonly IReadOnlyList<string> Report = ["abierto", "decidido", "en_apelacion"];

    public static bool IsReportState(string? state) => state is not null && Report.Contains(state, StringComparer.Ordinal);
}

/// <summary>Códigos de negocio de la spec 053 (§5): en inglés y en snake_case, sin datos de la persona.</summary>
internal static class ModerationErrorCodes
{
    public const string VerificationNotFoundCode = "verification_not_found";
    public const string PhotosDestroyedCode = "photos_destroyed";
    public const string AlreadyDecidedCode = "verification_already_decided";
    public const string ReportNotFoundCode = "report_not_found";
    public const string ReportAlreadyDecidedCode = "report_already_decided";
    public const string InvalidCursorCode = "invalid_review_cursor";
    public const string PhotosUnavailableCode = "photos_unavailable";
    public const string PhoneTakenCode = "phone_already_registered";
}

/// <summary>Nombres de los eventos de auditoría de esta spec (R-17 y R-22): sin celular, motivo ni datos de la persona.</summary>
internal static class ModerationAuditEvents
{
    public const string VerificationOpened = "verificacion_abierta";
    public const string VerificationDecided = "verificacion_decidida";
    public const string RecoveryDecided = "recuperacion_decidida";
    public const string ReportDecided = "reporte_decidido";
}

/// <summary>Reglas de entrada comunes de las colas y las decisiones (spec 053, R-16, R-18 y R-20).</summary>
internal static class ReviewRules
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;
    public const int MinReasonLength = 8;
    public const int MaxReasonLength = 300;

    /// <summary>Por omisión las colas miran los últimos 30 días (R-16, D-08).</summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(30);

    public static bool IsValidReason(string? reason)
    {
        var trimmed = reason?.Trim() ?? string.Empty;
        return trimmed.Length >= MinReasonLength && trimmed.Length <= MaxReasonLength && trimmed.All(c => c == (char)10 || !char.IsControl(c));
    }

    /// <summary>El rango efectivo de una cola: <c>desde</c> por omisión es hace 30 días y <c>hasta</c> es ahora (sin tope: la paginación acota el costo, D-08).</summary>
    public static (DateTime From, DateTime To) Range(DateTime? from, DateTime? to, DateTime now) => (from ?? now - DefaultWindow, to ?? now);
}

/// <summary>
/// Cursor opaco de una cola: la posición por gravedad (0 en la de verificaciones), la marca de tiempo y el <c>_id</c> del último elemento (base64url). Un valor que no se puede leer es un 400.
/// </summary>
internal readonly record struct ReviewCursor(int Rank, DateTime At, ObjectId Id)
{
    public static ReviewCursor? Parse(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
        {
            return null;
        }

        try
        {
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(cursor.Replace('-', '+').Replace('_', '/').PadRight(cursor.Length + ((4 - (cursor.Length % 4)) % 4), '=')));
            var parts = text.Split('|');
            if (parts.Length == 3 && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var rank) && rank <= ReportSeverities.All.Count
                && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) && parts[2].Length == 24 && ObjectId.TryParse(parts[2], out var id))
            {
                return new ReviewCursor(rank, new DateTime(ticks, DateTimeKind.Utc), id);
            }
        }
        catch (FormatException)
        {
            // Un cursor mal formado se trata igual que uno inválido: 400 sin repetir el valor.
        }
        catch (ArgumentOutOfRangeException)
        {
            // Ticks fuera de rango: mismo tratamiento.
        }

        throw new DomainException(DomainErrorKind.BadRequest, "The cursor is not valid.", ModerationErrorCodes.InvalidCursorCode);
    }

    public string Encode() =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(Rank.ToString(CultureInfo.InvariantCulture) + "|" + At.Ticks.ToString(CultureInfo.InvariantCulture) + "|" + Id)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
