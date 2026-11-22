using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Knock.Hosting.RateLimiting;

/// <summary>
/// Límite con contador compartido en Redis (ventana fija, un solo script atómico): dos copias del servicio comparten el mismo contador y no duplican el límite.
/// Si Redis no está conectado o no responde, la copia usa su propio contador en memoria (límite más laxo pero nunca sin límite) y deja el aviso 247.
/// La clave de Redis es un hash de la partición: nunca lleva el <c>sub</c> ni la IP en claro.
/// </summary>
internal sealed class SharedRateLimiter : RateLimiter
{
    /// <summary>Evento del aviso de degradación (KNOCK-247); no lleva datos de la petición.</summary>
    internal const int FallbackEventId = 247;

    private static readonly Action<ILogger, Exception?> _unavailable = LoggerMessage.Define(LogLevel.Warning, new EventId(FallbackEventId, "RateLimitFallback"), "Redis rate limiting unavailable; using per-instance limit.");

    private readonly IServiceProvider _services;
    private readonly ILogger _logger;
    private readonly string _key;
    private readonly RateLimitPolicySettings _policy;
    private readonly FixedWindowRateLimiter _fallback;

    public SharedRateLimiter(IServiceProvider services, string serviceName, string partition, RateLimitPolicySettings policy)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(policy);
        _services = services;
        _logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<SharedRateLimiter>();
        _key = $"ratelimit:{serviceName}:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(partition)));
        _policy = policy;
        _fallback = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = policy.PermitLimit,
            Window = policy.Window,
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    }

    public override TimeSpan? IdleDuration => _fallback.IdleDuration;

    public override RateLimiterStatistics? GetStatistics() => _fallback.GetStatistics();

    protected override RateLimitLease AttemptAcquireCore(int permitCount) => new SharedLease(false, _policy.Window);

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        var redis = _services.GetService<IConnectionMultiplexer>();
        if (redis is null || !redis.IsConnected)
        {
            return await UseFallbackAsync(permitCount, cancellationToken);
        }

        try
        {
            var (count, remaining) = await new RedisRequestCounter(redis).HitAsync(_key, _policy.Window, cancellationToken);
            return new SharedLease(count <= _policy.PermitLimit, remaining);
        }
        catch (Exception error) when (error is RedisException or TimeoutException)
        {
            return await UseFallbackAsync(permitCount, cancellationToken);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fallback.Dispose();
        }

        base.Dispose(disposing);
    }

    private ValueTask<RateLimitLease> UseFallbackAsync(int permitCount, CancellationToken cancellationToken)
    {
        _unavailable(_logger, null);
        return _fallback.AcquireAsync(permitCount, cancellationToken);
    }

    private sealed class SharedLease(bool acquired, TimeSpan remaining) : RateLimitLease
    {
        public override bool IsAcquired => acquired;

        public override IEnumerable<string> MetadataNames => [MetadataName.RetryAfter.Name];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = metadataName == MetadataName.RetryAfter.Name ? remaining : null;
            return metadata is not null;
        }
    }
}
