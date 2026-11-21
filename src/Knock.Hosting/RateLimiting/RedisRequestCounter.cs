using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Knock.Hosting.RateLimiting;

/// <summary>Contador de ventana fija compartido entre réplicas. Existe para probar el límite con dobles sin Redis.</summary>
public interface IRequestCounter
{
    /// <summary>Cuenta una petición en la ventana de <paramref name="key"/> y devuelve cuántas van y cuánto falta para que termine la ventana.</summary>
    Task<(long Count, TimeSpan Remaining)> HitAsync(string key, TimeSpan window, CancellationToken cancellationToken);
}

/// <summary>
/// Contadores en Redis (<c>ratelimit:*</c>) con un script: el incremento y la expiración de la ventana van juntos, así una caída entre los dos no deja
/// una clave sin vencimiento que bloquee a alguien para siempre.
/// </summary>
internal sealed class RedisRequestCounter(IConnectionMultiplexer redis) : IRequestCounter
{
    private const string _script = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then redis.call('PEXPIRE', KEYS[1], ARGV[1]) end
        return {count, redis.call('PTTL', KEYS[1])}
        """;

    public async Task<(long Count, TimeSpan Remaining)> HitAsync(string key, TimeSpan window, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = (long[]?)await redis.GetDatabase().ScriptEvaluateAsync(
            _script,
            [new RedisKey(key)],
            [new RedisValue(((long)window.TotalMilliseconds).ToString(CultureInfo.InvariantCulture))])
            ?? throw new InvalidOperationException("Redis returned no counter.");
        var remaining = result[1] > 0 ? TimeSpan.FromMilliseconds(result[1]) : window;
        return (result[0], remaining);
    }
}

public static class RequestCounterExtensions
{
    /// <summary>Registra el contador en Redis de <see cref="IRequestCounter"/> (el gateway lo usa en sus middlewares de límite).</summary>
    public static IServiceCollection AddKnockRequestCounter(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IRequestCounter, RedisRequestCounter>();
        return services;
    }
}
