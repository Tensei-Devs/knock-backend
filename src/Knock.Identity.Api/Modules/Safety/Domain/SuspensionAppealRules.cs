namespace Knock.Identity.Api.Modules.Safety.Domain;

/// <summary>Estados de una apelación de suspensión tal como los ve la persona (spec 060, R-09).</summary>
internal static class AppealStates
{
    public const string Pending = "pendiente";
    public const string Accepted = "aceptada";
    public const string Rejected = "rechazada";
}

/// <summary>De dónde viene una apelación guardada en <c>reportes.apelacion.origen</c> (spec 060, §6): la que hace una persona con sesión (spec 043) o la de una persona suspendida (esta spec).</summary>
internal static class AppealOrigins
{
    public const string Session = "sesion";
    public const string Suspension = "suspension";
}

/// <summary>Decisiones del panel sobre una apelación de suspensión (spec 060, §5).</summary>
internal static class AppealDecisions
{
    public const string Accept = "aceptar";
    public const string Reject = "rechazar";

    public static bool IsKnown(string? decision) => decision is Accept or Reject;
}

/// <summary>Registros de auditoría y eventos de la apelación (spec 060, R-06 y R-10). Solo identificadores: nunca el texto.</summary>
internal static class AppealEvents
{
    public const string AuditAccepted = "apelacion_aceptada";
    public const string AuditRejected = "apelacion_rechazada";
    public const string Resolved = "ApelacionResuelta";
}

/// <summary>Una apelación guardada en el reporte: texto, cuándo se presentó, de dónde viene y, si ya se resolvió, cómo y cuándo.</summary>
internal sealed record StoredAppeal(string Text, DateTime CreatedAtUtc, string Origin, string? Decision, DateTime? ResolvedAtUtc)
{
    /// <summary>El estado que ve la persona: pendiente hasta que el panel la resuelve.</summary>
    public string State => Decision switch
    {
        AppealDecisions.Accept => AppealStates.Accepted,
        AppealDecisions.Reject => AppealStates.Rejected,
        _ => AppealStates.Pending,
    };

    /// <summary>El texto no sale en ninguna representación: un registro lo imprimiría (spec 060, R-08).</summary>
    public sealed override string ToString() => nameof(StoredAppeal);
}

/// <summary>El reporte que causó la suspensión, tal como lo necesita la apelación: de quién es, su estado, quién decidió y la apelación que ya tiene.</summary>
internal sealed record ReportForAppeal(string ReportedId, string State, string? DeciderSub, StoredAppeal? Appeal);

/// <summary>Qué hacer con una petición de apelación (spec 060, R-04 y R-04a).</summary>
internal enum AppealDecision
{
    /// <summary>Guardar la apelación nueva.</summary>
    Open,

    /// <summary>Misma apelación y mismo texto mientras sigue pendiente: responde 200 con la que ya existe.</summary>
    Repeat,

    /// <summary>Otro texto, una apelación ya abierta, una ya rechazada o una de otro origen: 409 <c>appeal_not_allowed</c>.</summary>
    NotAllowed,

    /// <summary>No hay decisión sobre la que apelar: se responde como si no existiera (404 uniforme).</summary>
    NotFound,
}

/// <summary>Reglas puras de la apelación de una persona suspendida (spec 060). No tocan MongoDB, Redis ni el reloj.</summary>
internal static class SuspensionAppealRules
{
    /// <summary>
    /// Una sola vez por decisión y una abierta a la vez (R-04, R-04a). Sin apelación y con la decisión tomada se abre; la misma apelación pendiente con el mismo texto se repite sin duplicar; cualquier otra
    /// cosa (otro texto, ya rechazada, de otro origen) no se permite.
    /// </summary>
    public static AppealDecision Evaluate(ReportForAppeal report, string text)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.Appeal is null)
        {
            return report.State == ReportStates.Decided ? AppealDecision.Open : AppealDecision.NotFound;
        }

        var samePending = report.Appeal.Origin == AppealOrigins.Suspension
            && report.Appeal.Decision is null
            && string.Equals(report.Appeal.Text, text, StringComparison.Ordinal);
        return samePending ? AppealDecision.Repeat : AppealDecision.NotAllowed;
    }
}
