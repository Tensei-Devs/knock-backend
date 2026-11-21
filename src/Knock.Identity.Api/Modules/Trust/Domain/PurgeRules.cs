namespace Knock.Identity.Api.Modules.Trust.Domain;

/// <summary>Razones de una purga de fotos de verificación (spec 057, §5).</summary>
internal static class PurgeReasons
{
    public const string Decided = "decidida";
    public const string Expired = "vencida";
    public const string AccountDeletion = "baja_de_cuenta";

    public static bool IsKnown(string? reason) => reason is Decided or Expired or AccountDeletion;
}

/// <summary>
/// Reglas puras de la purga (spec 057, §4 y §5): los plazos fijados por la ficha HU-068 y el estado final de cada razón. La purga real (llave, blobs, auditoría) la hará
/// <c>PurgaDeVerificacion</c> cuando existan el almacén cifrado de la spec 025 y el ejecutor de la spec 054.
/// </summary>
internal static class PurgeRules
{
    /// <summary>La alerta al verificador sale a las 24 horas del envío (TC-HU-068-02).</summary>
    public static readonly TimeSpan AlertAfter = TimeSpan.FromHours(24);

    /// <summary>Sin revisión a los 7 días las fotos se borran (TC-HU-068-03).</summary>
    public static readonly TimeSpan ExpireAfter = TimeSpan.FromDays(7);

    public static DateTime AlertAt(DateTime sentAt) => sentAt + AlertAfter;

    public static DateTime ExpireAt(DateTime sentAt) => sentAt + ExpireAfter;

    /// <summary>Solo una verificación en revisión que aún no se alertó necesita la alerta de 24 horas.</summary>
    public static bool NeedsAlert(string state, DateTime? alertedAt, DateTime sentAt, DateTime now) =>
        state == VerificationStates.UnderReview && alertedAt is null && now >= AlertAt(sentAt);

    /// <summary>Solo una verificación en revisión y aún no purgada vence a los 7 días.</summary>
    public static bool IsExpired(string state, DateTime? purgedAt, DateTime sentAt, DateTime now) =>
        state == VerificationStates.UnderReview && purgedAt is null && now >= ExpireAt(sentAt);

    /// <summary>
    /// Estado con el que queda la verificación (§5): la decisión conserva el que decidió; el vencimiento y la baja la dejan <c>sin_verificar</c>. Una razón desconocida no purga.
    /// </summary>
    public static string? FinalState(string reason, bool approved) => reason switch
    {
        PurgeReasons.Decided => approved ? VerificationStates.Verified : VerificationStates.Rejected,
        PurgeReasons.Expired or PurgeReasons.AccountDeletion => VerificationStates.NotVerified,
        _ => null,
    };

    /// <summary>Motivo visible en el estado de la persona tras la purga: solo el vencimiento pide reenviar (<c>fotos_borradas</c>, <c>puedeReintentar</c>).</summary>
    public static string? VisibleReason(string reason) => reason == PurgeReasons.Expired ? VerificationReasons.PhotosDeleted : null;
}
