using System.Text.Json;
using Knock.Identity.Api.Modules.Usuarios.Domain;
using StackExchange.Redis;

namespace Knock.Identity.Api.Modules.Usuarios.Data;

/// <summary>Caché del perfil (spec 017, R-02 y R-07); existe para probar el servicio con dobles sin Redis.</summary>
internal interface IProfileCache
{
    Task<UserProfile?> GetAsync(string sub, CancellationToken cancellationToken);

    Task SetAsync(UserProfile profile, CancellationToken cancellationToken);

    /// <summary>Borra la clave. Si falla, el error sube: una escritura no puede responder dejando un perfil viejo en la caché.</summary>
    Task RemoveAsync(string sub, CancellationToken cancellationToken);
}

/// <summary>Clave <c>perfil:{sub}</c> en Redis con vida de 60 segundos. Leer y guardar fallan en silencio (se va a MongoDB); borrar no.</summary>
internal sealed partial class RedisProfileCache(IConnectionMultiplexer redis, ILogger<RedisProfileCache> logger) : IProfileCache
{
    public const string KeyPrefix = "perfil:";

    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    public static string Key(string sub) => KeyPrefix + sub;

    public async Task<UserProfile?> GetAsync(string sub, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var cached = await redis.GetDatabase().StringGetAsync(Key(sub));
            return cached.IsNullOrEmpty ? null : JsonSerializer.Deserialize<UserProfile>(cached.ToString());
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException or JsonException)
        {
            LogCacheUnavailable(logger);
            return null;
        }
    }

    public async Task SetAsync(UserProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await redis.GetDatabase().StringSetAsync(Key(profile.Id), JsonSerializer.Serialize(profile), Lifetime);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            LogCacheUnavailable(logger);
        }
    }

    public async Task RemoveAsync(string sub, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await redis.GetDatabase().KeyDeleteAsync(Key(sub));
    }

    [LoggerMessage(EventId = 2201, Level = LogLevel.Warning, Message = "Profile cache unavailable: reading from the database")]
    private static partial void LogCacheUnavailable(ILogger logger);
}
