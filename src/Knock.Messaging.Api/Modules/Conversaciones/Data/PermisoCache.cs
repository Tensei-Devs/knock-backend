using System.Text.Json;
using Knock.Messaging.Api.Modules.Conversaciones.Domain;
using StackExchange.Redis;

namespace Knock.Messaging.Api.Modules.Conversaciones.Data;

/// <summary>
/// El permiso vigente de una conversación en Redis durante 60 segundos (spec 030, R-09): <c>conv:permiso:{conversacionId}</c> con solo <c>abierta</c>, <c>motivoCierre</c>, <c>elegido</c> y
/// <c>visitaId</c>; nunca texto de mensajes. Un fallo de Redis no sale como excepción: se trata como «no hay permiso en caché» y el servicio pregunta a Mercado.
/// </summary>
internal sealed class PermisoCache(IConnectionMultiplexer redis)
{
    public const string KeyPrefix = "conv:permiso:";
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    public async Task<PermisoVigente?> GetAsync(string conversacionId)
    {
        try
        {
            var value = await redis.GetDatabase().StringGetAsync(KeyPrefix + conversacionId);
            return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<PermisoVigente>(value.ToString());
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException or JsonException)
        {
            return null;
        }
    }

    public async Task SetAsync(string conversacionId, PermisoVigente permiso)
    {
        ArgumentNullException.ThrowIfNull(permiso);
        try
        {
            await redis.GetDatabase().StringSetAsync(KeyPrefix + conversacionId, JsonSerializer.Serialize(permiso), Lifetime);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            // Sin caché el siguiente envío vuelve a preguntar a Mercado: no se pierde nada.
        }
    }
}
