using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Knock.Identity.Api.Common.Security;

/// <summary>
/// Token de servicio de Identidad hacia otro servicio (spec 037, R-11): HS256 con la clave de ese destino, <c>iss</c> <c>knock-identity</c>, <c>jti</c> único, <c>iat</c> y <c>exp</c> cortos y el claim
/// <c>permisos</c>. Los clientes internos (historial, entrega de la bandeja, reporte de «no terminado») lo comparten para no repetir la firma; los datos que acotan el token (cliente, visita) van en <c>extra</c>.
/// </summary>
internal static class ServiceTokens
{
    public const string Issuer = "knock-identity";

    public static string Issue(byte[] key, string audience, string[] permissions, DateTime nowUtc, TimeSpan lifetime, IReadOnlyDictionary<string, string>? extra = null)
    {
        var claims = new Dictionary<string, object> { ["jti"] = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)), ["permisos"] = permissions };
        foreach (var (name, value) in extra ?? new Dictionary<string, string>())
        {
            claims[name] = value;
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            Claims = claims,
            IssuedAt = nowUtc,
            NotBefore = nowUtc,
            Expires = nowUtc + lifetime,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256),
        });
    }
}
