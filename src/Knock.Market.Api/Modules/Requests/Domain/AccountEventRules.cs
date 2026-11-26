namespace Knock.Market.Api.Modules.Requests.Domain;

/// <summary>Cómo terminó de procesarse un evento de cuenta de Identidad.</summary>
internal enum AccountEventOutcome
{
    /// <summary>Se aplicó por primera vez.</summary>
    Applied,

    /// <summary>Ya se había procesado ese <c>eventoId</c>: no cambia nada (R-19).</summary>
    Duplicate,

    /// <summary>Un conflicto de escritura no se resolvió: la entrega se reintenta.</summary>
    Conflict,
}

/// <summary>Reglas del consumidor de la suspensión preventiva (spec 059, R-10, R-11 y R-19). Mercado solo conoce identificadores; nunca el motivo, el reporte ni quién reportó.</summary>
internal static class AccountEventRules
{
    public const string SuspendedEvent = "CuentaSuspendida";
    public const string ReactivatedEvent = "CuentaReactivada";

    /// <summary>Estado del perfil del técnico mientras su cuenta está suspendida (lo leen las specs 020 R-23 y 027 R-07).</summary>
    public const string SuspendedTechnicianState = "suspendido";

    public const string ActiveTechnicianState = "activo";

    /// <summary>Tipo del aviso al cliente con una cita próxima del técnico suspendido (spec 037, R-11): un aviso por cita.</summary>
    public const string AffectedVisitNotice = "visita_afectada";

    public const string ClientRole = "cliente";

    public const int MaxUserIdLength = 64;

    public static bool IsKnown(string? type) => type is SuspendedEvent or ReactivatedEvent;
}
