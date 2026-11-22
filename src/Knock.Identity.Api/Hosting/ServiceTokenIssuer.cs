using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Knock.Identity.Api.Hosting;

/// <summary>
/// Firma los tokens de servicio de Identidad hacia Mercado (spec 037, R-11): HS256 con la clave <c>interno-key-identity</c>, <c>iss</c> <c>knock-identity</c>, <c>aud</c> <c>knock-market</c>, <c>jti</c> único y el
/// claim <c>permisos</c>. Cada ruta interna añade los claims que atan el token a lo que pide (por ejemplo la persona), para que uno capturado no sirva con otros. Es el único lugar que firma estos tokens.
/// </summary>
internal static class ServiceTokenIssuer
{
    public const string MarketIssuer = "knock-identity";
    public const string MarketAudience = "knock-market";
    public const int MinKeyBytes = 32;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    /// <summary>La clave de la configuración si es Base64 de al menos 32 bytes; si no, <see langword="null"/> (sin clave ninguna ruta interna se llama).</summary>
    public static byte[]? ReadKey(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        try
        {
            var key = Convert.FromBase64String(configured);
            return key.Length >= MinKeyBytes ? key : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static string IssueForMarket(byte[] key, string permission, IReadOnlyDictionary<string, string> bindings, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(bindings);
        var claims = new Dictionary<string, object>
        {
            ["jti"] = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)),
            ["permisos"] = new[] { permission },
        };
        foreach (var binding in bindings)
        {
            claims[binding.Key] = binding.Value;
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = MarketIssuer,
            Audience = MarketAudience,
            Claims = claims,
            IssuedAt = nowUtc,
            NotBefore = nowUtc,
            Expires = nowUtc + Lifetime,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256),
        });
    }
}
