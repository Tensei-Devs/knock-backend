using Knock.Identity.Api.Modules.Sesiones.Domain;

namespace Knock.Identity.Api.Modules.Sesiones.Data;

/// <summary>Lo que el canje necesita de las colecciones <c>usuarios</c> y <c>sesiones</c>; existe para probar el flujo con dobles sin MongoDB.</summary>
internal interface ISesionesStore
{
    Task<UserAccount?> FindBySubAsync(string sub, CancellationToken cancellationToken);

    Task<UserAccount?> FindByPhoneHashAsync(byte[] phoneHash, CancellationToken cancellationToken);

    Task<AccountCreation> CreateAccountAsync(NewAccount account, CancellationToken cancellationToken);

    /// <summary>Abre una sesión de una cuenta que ya existe y, si la cuenta pasa de <see cref="NewSession.MaxOpenPerAccount"/>, revoca la más antigua.</summary>
    Task AddSessionAsync(NewSession session, CancellationToken cancellationToken);

    Task<UserAccount?> FindByIdAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Sesión cuya huella vigente es <paramref name="fingerprint"/>.</summary>
    Task<StoredSession?> FindSessionByFingerprintAsync(byte[] fingerprint, CancellationToken cancellationToken);

    /// <summary>Sesión que ya rotó a otro token pero conserva <paramref name="fingerprint"/> entre las últimas usadas (señal de token robado).</summary>
    Task<StoredSession?> FindSessionByUsedFingerprintAsync(byte[] fingerprint, CancellationToken cancellationToken);

    /// <summary>
    /// Rota la huella en una sola operación condicionada a la huella vigente, a que la sesión no esté revocada y a que no haya vencido: de dos
    /// peticiones con el mismo token solo una devuelve <c>true</c>. La huella anterior pasa a las usadas (las últimas 5).
    /// </summary>
    Task<bool> RotateAsync(string sessionId, byte[] currentFingerprint, byte[] newFingerprint, DateTime now, DateTime newExpiresAt, CancellationToken cancellationToken);

    /// <summary>Revoca todas las sesiones abiertas de la cuenta y devuelve sus ids (<c>sid</c>) para cancelarlos en Redis.</summary>
    Task<IReadOnlyList<string>> RevokeAllAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Revoca la sesión <paramref name="sessionId"/> si pertenece al <paramref name="sub"/>; <c>false</c> si no existe o es de otra cuenta.</summary>
    Task<bool> RevokeAsync(string sessionId, string sub, CancellationToken cancellationToken);
}
