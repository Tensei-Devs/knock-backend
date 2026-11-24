using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Knock.Hosting.Security;

/// <summary>Parámetros del JWT propio: los comparten el emisor (Identidad) y la validación de los demás servicios y del gateway.</summary>
public sealed record JwtSettings(string Issuer, string Audience, SymmetricSecurityKey SigningKey)
{
    /// <summary>
    /// Parámetros de validación del JWT propio: un solo algoritmo (HMAC-SHA256), firmante, destinatario y vencimiento obligatorios y sin tolerancia de reloj.
    /// </summary>
    public TokenValidationParameters ToValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = SigningKey,
        RequireSignedTokens = true,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.Zero,
    };
}

/// <summary>
/// Lee <c>Jwt:Issuer</c>, <c>Jwt:Audience</c> y <c>Jwt:SigningKey</c> (Base64, al menos 32 bytes) de user-secrets o variables de entorno.
/// Con entorno, solo en Development, si falta la clave, se genera una aleatoria al arrancar; fuera de Development el servicio no arranca sin ella.
/// <see cref="CreateRequiringKey"/> (el gateway) exige la clave en todos los entornos.
/// </summary>
public static class JwtSettingsFactory
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

    /// <summary>Igual que <see cref="Create"/> pero sin clave aleatoria en Development: la clave es obligatoria siempre.</summary>
    public static JwtSettings CreateRequiringKey(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var issuer = Required(configuration, "Jwt:Issuer");
        var audience = Required(configuration, "Jwt:Audience");
        return new JwtSettings(issuer, audience, new SymmetricSecurityKey(DecodeKey(Required(configuration, "Jwt:SigningKey"))));
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

        return DecodeKey(configured);
    }

    private static byte[] DecodeKey(string configured)
    {
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
