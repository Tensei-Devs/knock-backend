using Knock.Messaging.Api.Modules.Avisos.Push;
using StackExchange.Redis;

namespace Knock.Messaging.Api.Modules.Avisos.Data;

/// <summary>
/// Lee la presencia en vivo <c>chat:presencia:{sub}</c> (spec 037, R-08; la escribe el hub del chat, spec 030 R-15). Si Redis no responde se trata como ausente: se envía el aviso (un aviso de más
/// es preferible a perder uno).
/// </summary>
internal sealed class RedisPresenceReader(IConnectionMultiplexer redis) : IPresenceReader
{
    public const string KeyPrefix = "chat:presencia:";

    public async Task<bool> IsPresentAsync(string person, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(person);
        try
        {
            return await redis.GetDatabase().KeyExistsAsync(KeyPrefix + person).WaitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            return false;
        }
    }
}
