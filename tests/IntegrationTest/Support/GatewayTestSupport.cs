using System.Security.Cryptography;
using Knock.Gateway.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Knock.IntegrationTest.Support;

/// <summary>Piezas de las pruebas del gateway: un JWT como el de Identidad (HS256, <c>sub</c> y <c>sid</c>) y dobles de las dependencias de Redis.</summary>
internal static class GatewayTestSupport
{
    public const string Issuer = "knock-api";
    public const string Audience = "knock-app";

    /// <summary>Clave sintética: 32 bytes aleatorios por proceso; nunca se escribe en disco.</summary>
    public static readonly byte[] SigningKey = RandomNumberGenerator.GetBytes(32);

    public static string SigningKeyBase64 => Convert.ToBase64String(SigningKey);

    /// <summary>Cadena sintética válida para el arranque (con contraseña); las pruebas que no usan Redis nunca llegan a conectar.</summary>
    public const string DummyRedisConnectionString = "127.0.0.1:6390,password=sintetica";

    public static string IssueToken(
        string? sub = null,
        string? sid = null,
        byte[]? key = null,
        string algorithm = SecurityAlgorithms.HmacSha256,
        string audience = Audience,
        TimeSpan? lifetime = null)
    {
        var now = DateTime.UtcNow;
        var expires = now + (lifetime ?? TimeSpan.FromMinutes(15));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = sub ?? Guid.NewGuid().ToString("D"),
                ["sid"] = sid ?? Guid.NewGuid().ToString("N")[..24],
            },
            IssuedAt = lifetime < TimeSpan.Zero ? now.AddHours(-1) : now,
            NotBefore = lifetime < TimeSpan.Zero ? now.AddHours(-1) : now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key ?? SigningKey), algorithm),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}

/// <summary>Ninguna sesión cancelada: para las pruebas de enrutamiento, que no usan Redis.</summary>
internal sealed class NoRevokedSessions : IRevokedSessions
{
    public Task<bool> IsRevokedAsync(string sessionId, CancellationToken cancellationToken) => Task.FromResult(false);
}

/// <summary>Sin límite: para las pruebas de enrutamiento, que no usan Redis.</summary>
internal sealed class NoLimitCounter : IRequestCounter
{
    public Task<(long Count, TimeSpan Remaining)> HitAsync(string key, TimeSpan window, CancellationToken cancellationToken) =>
        Task.FromResult((1L, window));
}

/// <summary>Redis no responde: el gateway debe cerrar el paso con 503 y no dejarlo abierto.</summary>
internal sealed class UnavailableRevokedSessions : IRevokedSessions
{
    public Task<bool> IsRevokedAsync(string sessionId, CancellationToken cancellationToken) => throw new TimeoutException("synthetic");
}

internal sealed class UnavailableCounter : IRequestCounter
{
    public Task<(long Count, TimeSpan Remaining)> HitAsync(string key, TimeSpan window, CancellationToken cancellationToken) => throw new TimeoutException("synthetic");
}
