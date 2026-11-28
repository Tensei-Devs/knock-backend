using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Knock.Identity.Api.Modules.Sesiones.Domain;

namespace Knock.Identity.Api.Modules.Sesiones.Services;

/// <summary>
/// Verifica el ID token con el Firebase Admin SDK (<c>VerifyIdTokenAsync</c>): firma, vencimiento, emisor y proyecto.
/// El <c>uid</c> y el celular salen solo del token y se exige <c>phone_number</c> válido (+52). Con la variable del emulador (solo Development) el SDK no comprueba la firma.
/// La verificación usa llaves públicas de Google: no necesita cuenta de servicio.
/// </summary>
internal sealed class FirebaseIdTokenVerifier(IConfiguration configuration) : IFirebaseIdTokenVerifier, IDisposable
{
    private readonly Lazy<FirebaseApp> _app = new(() => CreateApp(configuration), LazyThreadSafetyMode.ExecutionAndPublication);

    public async Task<FirebaseIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken)
    {
        try
        {
            var token = await FirebaseAuth.GetAuth(_app.Value).VerifyIdTokenAsync(idToken, cancellationToken);
            return token.Claims.TryGetValue("phone_number", out var phone) && phone is string number && PhoneNumberPolicy.IsValid(number)
                ? new FirebaseIdentity(token.Uid, number, ReadAuthTime(token.Claims))
                : null;
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode != AuthErrorCode.CertificateFetchFailed)
        {
            return null;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or Newtonsoft.Json.JsonException)
        {
            // Token vacío o mal formado (el SDK deja pasar errores de Base64 y de JSON de su propio lector).
            return null;
        }
    }

    // `auth_time` (segundos Unix del último inicio de sesión) lo pone Firebase; sin él o ilegible la identidad no es «reciente».
    private static DateTimeOffset? ReadAuthTime(IReadOnlyDictionary<string, object> claims)
    {
        if (!claims.TryGetValue("auth_time", out var value) || value is null)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_app.IsValueCreated)
        {
            _app.Value.Delete();
        }
    }

    private static FirebaseApp CreateApp(IConfiguration configuration)
    {
        var projectId = configuration["Firebase:ProjectId"];
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new InvalidOperationException("Firebase:ProjectId is required.");
        }

        // Una app por instancia (nombre único): no hay estado global compartido entre hosts de prueba.
        return FirebaseApp.Create(
            new AppOptions
            {
                ProjectId = projectId,
                Credential = GoogleCredential.FromAccessToken("not-used-to-verify-id-tokens"),
            },
            $"knock-identity-{Guid.NewGuid():N}");
    }
}
