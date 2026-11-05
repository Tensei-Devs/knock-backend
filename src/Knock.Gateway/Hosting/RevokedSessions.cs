using StackExchange.Redis;

namespace Knock.Gateway.Hosting;

/// <summary>
/// Sesiones canceladas por el logout o por el reuso de un token de renovación (specs 009, R-08, y 016). Existe para probar el gateway con dobles
/// sin Redis. Solo lee: quien escribe la clave es Identidad.
/// </summary>
internal interface IRevokedSessions
{
    Task<bool> IsRevokedAsync(string sessionId, CancellationToken cancellationToken);
}

/// <summary>Consulta <c>sesion-cancelada:{sid}</c> en Redis, la misma clave que escribe Identidad.</summary>
internal sealed class RedisRevokedSessions(IConnectionMultiplexer redis) : IRevokedSessions
{
    public const string KeyPrefix = "sesion-cancelada:";

    public async Task<bool> IsRevokedAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        cancellationToken.ThrowIfCancellationRequested();
        return await redis.GetDatabase().KeyExistsAsync(KeyPrefix + sessionId);
    }
}
