// jscpd:ignore-start -- EXC-021: copia de src/Knock.Market.Api/Common/Security/JwtSettings*.cs (KNOCK-241); se unifica en la biblioteca técnica de KNOCK-251.
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Knock.Messaging.Api.Hosting;

/// <summary>Parámetros del JWT propio: los comparte el emisor (Identity) y la validación de este servicio.</summary>
internal sealed record JwtSettings(string Issuer, string Audience, SymmetricSecurityKey SigningKey);

/// <summary>
/// Lee <c>Jwt:Issuer</c>, <c>Jwt:Audience</c> y <c>Jwt:SigningKey</c> (Base64, al menos 32 bytes) de user-secrets o variables de entorno.
/// Solo en Development, si falta la clave, se genera una aleatoria al arrancar; fuera de Development el servicio no arranca sin ella.
/// </summary>
internal static class JwtSettingsFactory
{
    public const int MinimumKeyBytes = 32;

    public static JwtSettings Create(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var issuer = Required(configuration, "Jwt:Issuer");
        var audience = Required(configuration, "Jwt:Audience");
        return new JwtSettings(issuer, audience, new SymmetricSecurityKey(ResolveKey(configuration["Jwt:SigningKey"], environment)));
    }

    private static string Required(IConfiguration configuration, string key) =>
        string.IsNullOrWhiteSpace(configuration[key])
            ? throw new InvalidOperationException($"{key} is required.")
            : configuration[key]!;

    private static byte[] ResolveKey(string? configured, IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return environment.IsDevelopment()
                ? RandomNumberGenerator.GetBytes(MinimumKeyBytes)
                : throw new InvalidOperationException(
                    $"Jwt:SigningKey is required outside Development (Base64, at least {MinimumKeyBytes} bytes): set it with an environment variable or a secret store.");
        }

        var buffer = new byte[configured.Length];
        if (!Convert.TryFromBase64String(configured, buffer, out var written))
        {
            throw new InvalidOperationException("Jwt:SigningKey must be Base64.");
        }

        return written >= MinimumKeyBytes
            ? buffer[..written]
            : throw new InvalidOperationException($"Jwt:SigningKey must decode to at least {MinimumKeyBytes} bytes.");
    }
}
// jscpd:ignore-end -- EXC-021
