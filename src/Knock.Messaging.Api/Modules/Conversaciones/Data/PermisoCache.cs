using System.Text.Json;
using StackExchange.Redis;

namespace Knock.Messaging.Api.Modules.Conversaciones.Data;

/// <summary>Lo único del permiso que se guarda en Redis (spec 030, R-09): nunca texto de mensajes.</summary>
internal sealed record PermisoVigente(bool Abierta, string? MotivoCierre, bool Elegido, string? VisitaId);

/// <summary>
/// El permiso vigente de una conversación en Redis durante 60 segundos (spec 030, R-09): <c>conv:permiso:{conversacionId}</c>. Un fallo de Redis no sale como excepción: se trata como «no hay
/// permiso en caché» y quien llama falla cerrado.
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
