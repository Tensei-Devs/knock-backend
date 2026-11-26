namespace Knock.Identity.Api.Modules.Sesiones.Services;

/// <summary>
/// Identidad verificada por Firebase: el <c>uid</c> y el celular E.164 (<c>+52</c>) del token. Salen solo de aquí: el cliente nunca los manda.
/// <see cref="ToString"/> no los incluye para que ningún registro los reciba por accidente.
/// </summary>
internal sealed record FirebaseIdentity(string Uid, string PhoneNumber, DateTimeOffset? AuthTime = null)
{
    /// <summary>Antigüedad máxima del código SMS para las rutas que piden una prueba de control del número reciente (spec 060, R-01).</summary>
    public static readonly TimeSpan RecentSignInWindow = TimeSpan.FromMinutes(10);

    public override string ToString() => nameof(FirebaseIdentity);

    /// <summary>
    /// Si el inicio de sesión por SMS es de hasta <paramref name="window"/> atrás (<c>auth_time</c> del token). Sin <c>auth_time</c> o con uno en el futuro (más de un minuto de desfase de reloj)
    /// no es reciente: la prueba de control del número no se concede por omisión.
    /// </summary>
    public bool IsRecent(DateTimeOffset now, TimeSpan window) => AuthTime is { } at && at <= now + TimeSpan.FromMinutes(1) && now - at <= window;
}

/// <summary>Valida un ID token de Firebase. Devuelve <c>null</c> por cualquier motivo de rechazo (sin distinguirlos).</summary>
internal interface IFirebaseIdTokenVerifier
{
    Task<FirebaseIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken);
}
