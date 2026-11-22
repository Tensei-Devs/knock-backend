using System.Globalization;

namespace Knock.Market.Api.Modules.Requests.Domain;

/// <summary><c>code</c> de negocio de la agenda y los cambios de la visita (spec 050, §5): en inglés y en <c>snake_case</c>, sin datos de la persona.</summary>
internal static class VisitScheduleErrorCodes
{
    public const string VisitNotReschedulableCode = "visit_not_reschedulable";
    public const string NoPendingProposalCode = "no_pending_proposal";
    public const string VisitAlreadyClosedCode = "visit_already_closed";
    public const string OutsideArrivalWindowCode = "outside_arrival_window";
    public const string VisitNotInProgressCode = "visit_not_in_progress";
    public const string ReportWindowClosedCode = "report_window_closed";
}

/// <summary>Reglas puras de agenda, reprogramación, cancelación y cliente ausente (spec 050, R-24 a R-27). La cita se guarda como día y hora de <c>America/Mexico_City</c>.</summary>
internal static class VisitScheduleRules
{
    public const string DayFormat = "yyyy-MM-dd";
    public const string TimeFormat = "HH:mm";
    public const string Cancelled = "cancelada";
    public const string NoShow = "no_presentado";
    public const string ClientAbsent = "cliente";
    public const string ProposedByClient = "cliente";
    public const string ProposedByTechnician = "tecnico";
    public const string ProposalExpiryTask = "VencerPropuestaDeReprogramacion";

    public const int DefaultAgendaDays = 14;
    public const int MaxAgendaDays = 31;
    public const int MaxAgendaLimit = 50;
    public const int DefaultAgendaLimit = 20;
    public const int MaxReschedules = 2;
    public const int MinReasonLength = 8;
    public const int MaxReasonLength = 300;

    public static readonly TimeSpan MinRescheduleLead = TimeSpan.FromHours(2);
    public static readonly TimeSpan MaxRescheduleLead = TimeSpan.FromDays(30);
    public static readonly TimeSpan ProposalValidity = TimeSpan.FromHours(24);
    public static readonly TimeSpan ProposalCloseToVisit = TimeSpan.FromHours(2);
    public static readonly TimeSpan FreeCancellationLead = TimeSpan.FromHours(24);
    public static readonly TimeSpan AbsenceOpensAfter = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan AbsenceClosesAfter = TimeSpan.FromHours(2);

    private const string _timeZone = "America/Mexico_City";

    private static TimeZoneInfo Zone => TimeZoneInfo.FindSystemTimeZoneById(_timeZone);

    /// <summary>Instante UTC de la cita guardada como día (<c>yyyy-MM-dd</c>) y hora (<c>HH:mm</c>) de la Ciudad de México.</summary>
    public static DateTime AppointmentUtc(string day, string time)
    {
        var date = DateOnly.ParseExact(day, DayFormat, CultureInfo.InvariantCulture);
        var hour = TimeOnly.ParseExact(time, TimeFormat, CultureInfo.InvariantCulture);
        return TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(hour, DateTimeKind.Unspecified), Zone);
    }

    /// <summary>Día y hora locales de un instante UTC, en el formato guardado.</summary>
    public static (string Day, string Time) ToLocal(DateTime utc)
    {
        var local = ToLocalDateTime(utc);
        return (local.ToString(DayFormat, CultureInfo.InvariantCulture), local.ToString(TimeFormat, CultureInfo.InvariantCulture));
    }

    /// <summary>Día local de hoy, para el rango por omisión de la agenda (R-24).</summary>
    public static DateOnly LocalToday(DateTime nowUtc) => DateOnly.FromDateTime(ToLocalDateTime(nowUtc));

    /// <summary>Verdadero si <paramref name="value"/> es un día <c>yyyy-MM-dd</c> real.</summary>
    public static bool TryParseDay(string? value, out DateOnly day) =>
        DateOnly.TryParseExact(value, DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    /// <summary>Rango válido de la agenda: <c>hasta</c> no anterior a <c>desde</c> y como máximo 31 días de rango (R-24).</summary>
    public static bool IsValidAgendaRange(DateOnly from, DateOnly to) => to >= from && to.DayNumber - from.DayNumber < MaxAgendaDays;

    /// <summary>Verdadero si el instante propuesto está al menos 2 horas adelante y como máximo a 30 días (R-25).</summary>
    public static bool IsValidProposal(DateTime proposedUtc, DateTime nowUtc) => proposedUtc >= nowUtc + MinRescheduleLead && proposedUtc <= nowUtc + MaxRescheduleLead;

    /// <summary>
    /// Vencimiento de la propuesta: 24 horas, o 2 horas antes de la cita si es menos (R-25). <see langword="null"/> si ese momento ya pasó: con la cita tan cerca no hay tiempo para responder.
    /// </summary>
    public static DateTime? ProposalExpiresAt(DateTime nowUtc, DateTime appointmentUtc)
    {
        var expires = nowUtc + ProposalValidity;
        var beforeVisit = appointmentUtc - ProposalCloseToVisit;
        var earliest = expires < beforeVisit ? expires : beforeVisit;
        return earliest > nowUtc ? earliest : null;
    }

    /// <summary>Una cancelación es tardía si faltan menos de 24 horas para la cita (R-26).</summary>
    public static bool IsLateCancellation(DateTime nowUtc, DateTime appointmentUtc) => appointmentUtc - nowUtc < FreeCancellationLead;

    /// <summary>Ventana del cliente ausente: desde 15 minutos después de la cita y hasta 2 horas después (R-27).</summary>
    public static bool IsInAbsenceWindow(DateTime nowUtc, DateTime appointmentUtc) =>
        nowUtc >= appointmentUtc + AbsenceOpensAfter && nowUtc <= appointmentUtc + AbsenceClosesAfter;

    /// <summary>El motivo escrito por la persona mide de 8 a 300 caracteres sin contar espacios de los extremos (R-26).</summary>
    public static bool IsValidReason(string? reason) => reason?.Trim().Length is >= MinReasonLength and <= MaxReasonLength;

    private static DateTime ToLocalDateTime(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
}
