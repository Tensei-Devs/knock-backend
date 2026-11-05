using StackExchange.Redis;

namespace Knock.Identity.Api.Modules.Sesiones.Data;

/// <summary>
/// Lista de sesiones canceladas (spec 016): el gateway rechaza cualquier JWT cuyo <c>sid</c> esté aquí. Existe para probar los servicios con dobles
/// sin Redis.
/// </summary>
internal interface ISesionCancelada
{
    /// <summary>Marca la sesión como cancelada durante <paramref name="lifetime"/>, el resto de vigencia del JWT más largo que pudo emitirse.</summary>
    Task CancelAsync(string sessionId, TimeSpan lifetime, CancellationToken cancellationToken);
}

/// <summary>Clave <c>sesion-cancelada:{sid}</c> en Redis con tiempo de vida: la lista nunca crece más de lo que se emite en 15 minutos.</summary>
internal sealed class RedisSesionCancelada(IConnectionMultiplexer redis) : ISesionCancelada
{
    public const string KeyPrefix = "sesion-cancelada:";

    public static string Key(string sessionId) => KeyPrefix + sessionId;

    public async Task CancelAsync(string sessionId, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        cancellationToken.ThrowIfCancellationRequested();
        await redis.GetDatabase().StringSetAsync(Key(sessionId), "1", lifetime);
    }
}
