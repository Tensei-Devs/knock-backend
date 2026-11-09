using Microsoft.Extensions.Configuration;

namespace Knock.SecurityTest.Support;

/// <summary>
/// Configuración común de los hosts de seguridad (Identity y Market): emisor, audiencia, llave de firma del JWT y límites altos
/// para que el limitador de peticiones no estorbe a las pruebas que no lo prueban. Cada host agrega lo suyo en <c>extra</c>.
/// </summary>
internal static class SecurityHostConfiguration
{
    /// <summary>
    /// El CI define FIREBASE_AUTH_EMULATOR_HOST para las pruebas de integración y tanto Identidad como Mercado se niegan a arrancar fuera de Development con ella
    /// (el emulador emite tokens sin firmar). Estas pruebas usan un verificador de Firebase falso: se quita antes de crear cualquier host, sin importar el orden de las pruebas.
    /// </summary>
    public static void ClearFirebaseEmulatorHost() => Environment.SetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST", null);

    public static void Apply(
        IConfigurationBuilder configuration,
        string highLimit,
        IReadOnlyDictionary<string, string?> overrides,
        IReadOnlyDictionary<string, string?>? extra = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(overrides);

        var values = new Dictionary<string, string?>
        {
            ["Firebase:ProjectId"] = "demo-knock",
            ["Jwt:Issuer"] = TokenForge.Issuer,
            ["Jwt:Audience"] = TokenForge.Audience,
            ["Jwt:SigningKey"] = Convert.ToBase64String(TokenForge.SigningKey),
            ["RateLimiting:Global:PermitLimit"] = highLimit,
            ["RateLimiting:Auth:PermitLimit"] = highLimit,
        };

        foreach (var (key, value) in extra ?? new Dictionary<string, string?>())
        {
            values[key] = value;
        }

        foreach (var (key, value) in overrides)
        {
            values[key] = value;
        }

        configuration.AddInMemoryCollection(values);
    }
}
