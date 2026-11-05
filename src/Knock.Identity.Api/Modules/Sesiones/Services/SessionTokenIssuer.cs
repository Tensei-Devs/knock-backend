using System.Security.Cryptography;
using Knock.Identity.Api.Common.Security;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Knock.Identity.Api.Modules.Sesiones.Services;

/// <summary>Sesión emitida: JWT de acceso y token de renovación (valor aleatorio; en la base solo se guarda su huella).</summary>
internal sealed record IssuedSession(string AccessToken, string RefreshToken);

/// <summary>
/// Emite el JWT propio (HS256, 15 min, claims <c>sub</c> y <c>sid</c>, sin teléfono ni <c>role</c>) y el token de renovación de 256 bits
/// (spec 015, R-08 y R-09). El <c>sid</c> es el id de la sesión: el gateway cancela por él (specs 009 y 016).
/// </summary>
internal sealed class SessionTokenIssuer(JwtSettings settings, TimeProvider timeProvider)
{
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    private const int _refreshTokenBytes = 32;

    public IssuedSession Issue(Guid sub, string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = sub.ToString("D"),
                ["sid"] = sessionId,
            },
            IssuedAt = now,
            NotBefore = now,
            Expires = now + AccessTokenLifetime,
            SigningCredentials = new SigningCredentials(settings.SigningKey, SecurityAlgorithms.HmacSha256),
        };

        var accessToken = new JsonWebTokenHandler().CreateToken(descriptor);
        var refreshToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(_refreshTokenBytes));
        return new IssuedSession(accessToken, refreshToken);
    }
}
