namespace Knock.Identity.Api.Modules.Sesiones.Domain;

/// <summary>Estados de una cuenta (spec 015, R-07). Una cuenta en <see cref="DeletionScheduled"/> sí inicia sesión.</summary>
internal static class AccountState
{
    public const string Active = "activa";
    public const string Suspended = "suspendida";
    public const string DeletionScheduled = "eliminacion_programada";
}

/// <summary>Documentos que la persona acepta al registrarse (spec 015, R-04). Sus versiones salen de la configuración (<c>Consentimientos</c>).</summary>
internal static class ConsentDocuments
{
    public const string PrivacyNotice = "aviso_privacidad";
    public const string Terms = "terminos";
}

/// <summary>Cuenta tal como la necesita el canje: sin el celular, solo el hash para compararlo.</summary>
/// <param name="Id"><c>_id</c> (ObjectId en hexadecimal); es interno del servicio y no sale en ninguna respuesta.</param>
/// <param name="Sub">Identificador público de la cuenta (UUID v8, 36 caracteres).</param>
/// <param name="State"><see cref="AccountState"/>.</param>
/// <param name="ProfileComplete">Si ya completó el perfil (lo cambia la spec 017).</param>
/// <param name="PhoneHash">HMAC-SHA256 del celular.</param>
internal sealed record UserAccount(string Id, string Sub, string State, bool ProfileComplete, byte[] PhoneHash);

/// <summary>Sesión de un dispositivo (spec 015, R-08): el token de renovación nunca se guarda, solo su SHA-256.</summary>
/// <param name="Id"><c>_id</c> de la sesión (ObjectId en hexadecimal): es el <c>sid</c> del JWT.</param>
/// <param name="UserId"><c>_id</c> del usuario dueño de la sesión.</param>
/// <param name="Sub"><c>sub</c> del usuario.</param>
/// <param name="DeviceId">UUID que genera la app; no se autentica.</param>
/// <param name="DeviceName">Nombre del teléfono, máximo 80 caracteres.</param>
/// <param name="Fingerprint">SHA-256 de los 32 bytes del token de renovación.</param>
/// <param name="CreatedAt">Fecha UTC de creación; vence 30 días después.</param>
internal sealed record NewSession(string Id, string UserId, string Sub, string DeviceId, string DeviceName, byte[] Fingerprint, DateTime CreatedAt)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    /// <summary>Sesiones abiertas por cuenta; al abrir una más se revoca la más antigua (R-10).</summary>
    public const int MaxOpenPerAccount = 10;

    public DateTime ExpiresAt => CreatedAt + Lifetime;
}

/// <summary>Alta de una cuenta nueva: se guarda entera o no se guarda nada (usuario con sus dos consentimientos y la primera sesión).</summary>
internal sealed record NewAccount(
    string Id,
    string Sub,
    byte[] PhoneCiphertext,
    int PhoneCiphertextVersion,
    byte[] PhoneHash,
    int PhoneHashVersion,
    string PrivacyNoticeVersion,
    string TermsVersion,
    DateTime CreatedAt,
    NewSession Session);

/// <summary>Resultado del alta: si ya existía el <c>sub</c> o el celular, un índice único lo rechazó y no se guardó nada.</summary>
internal enum AccountCreation
{
    Created,
    AlreadyExists,
}

/// <summary>Sesión guardada, tal como la necesitan la renovación y el cierre (spec 016): sin la huella ni el token.</summary>
/// <param name="Id"><c>_id</c> de la sesión (el <c>sid</c> del JWT).</param>
/// <param name="UserId"><c>_id</c> del usuario dueño.</param>
/// <param name="Sub"><c>sub</c> del usuario.</param>
/// <param name="Revoked">Si el logout, el reuso o el límite de sesiones la revocó.</param>
/// <param name="ExpiresAt">Fecha UTC en que vence; se mueve 30 días en cada renovación.</param>
internal sealed record StoredSession(string Id, string UserId, string Sub, bool Revoked, DateTime ExpiresAt);
