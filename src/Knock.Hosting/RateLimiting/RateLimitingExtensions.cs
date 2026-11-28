using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Knock.Hosting.RateLimiting;

/// <summary>Lo que cambia entre servicios en el limitador por IP: el nombre (prefijo de las claves de Redis), el cuerpo del 429 y las políticas por nombre.</summary>
public sealed class KnockRateLimitingOptions
{
    /// <summary>Prefijo de las claves de Redis (<c>ratelimit:{ServiceName}:…</c>).</summary>
    public required string ServiceName { get; init; }

    /// <summary>Escribe el 429 (ProblemDetails del servicio); el cuerpo nunca revela IP, clave, límite ni contador.</summary>
    public required Func<HttpContext, Task> WriteTooManyRequestsAsync { get; init; }

    /// <summary>Declara la política estricta de <c>/api/v{n}/auth/*</c> (sección <c>RateLimiting:Auth</c>).</summary>
    public bool StrictAuth { get; init; }

    /// <summary>Rutas extra con el límite estricto (por ejemplo, la sesión del panel en Identity); recibe los segmentos de la ruta.</summary>
    public Func<string[], bool>? IsExtraStrictPath { get; init; }

    /// <summary>Si es <c>false</c>, el límite general y el de <c>/health/ready</c> viven en memoria por copia (comportamiento de Mensajería antes de la etapa 2).</summary>
    public bool SharedGlobalCounters { get; init; } = true;

    /// <summary>Políticas por nombre del servicio (<c>[EnableRateLimiting]</c>).</summary>
    public Action<KnockRateLimitPolicies>? ConfigurePolicies { get; init; }
}

/// <summary>Registro de políticas por nombre; todas usan el contador compartido en Redis con respaldo por copia.</summary>
public sealed class KnockRateLimitPolicies
{
    private readonly RateLimiterOptions _options;
    private readonly IServiceProvider _services;
    private readonly string _serviceName;

    internal KnockRateLimitPolicies(RateLimiterOptions options, IServiceProvider services, string serviceName)
    {
        _options = options;
        _services = services;
        _serviceName = serviceName;
    }

    /// <summary>Política por <c>sub</c> del token; sin <c>sub</c> no limita (la autenticación ya respondió).</summary>
    public KnockRateLimitPolicies PerSubject(string name, int permitLimit, TimeSpan window)
    {
        var policy = new RateLimitPolicySettings { PermitLimit = permitLimit, Window = window };
        _options.AddPolicy(name, context => context.User.FindFirst("sub")?.Value is { Length: > 0 } subject
            ? RateLimitPartition.Get($"{name}:{subject}", partition => new SharedRateLimiter(_services, _serviceName, partition, policy))
            : RateLimitPartition.GetNoLimiter(RequestKeys.UnknownClient));
        return this;
    }

    /// <summary>
    /// Política por una clave que el servicio saca de la petición (por ejemplo, el <c>sid</c> del token del panel, que el limitador aún no ve autenticado). Sin clave, o con una clave que
    /// <paramref name="keySelector"/> no acepta, cuenta por IP: una petición sin credencial nunca queda sin límite. La clave solo reparte el contador; no autentica a nadie.
    /// </summary>
    public KnockRateLimitPolicies PerKey(string name, Func<HttpContext, string?> keySelector, int permitLimit, TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        var policy = new RateLimitPolicySettings { PermitLimit = permitLimit, Window = window };
        _options.AddPolicy(name, context =>
        {
            var partition = keySelector(context) is { Length: > 0 } key ? $"{name}:{key}" : $"{name}:ip:{RequestKeys.ClientKey(context)}";
            return RateLimitPartition.Get(partition, partitionKey => new SharedRateLimiter(_services, _serviceName, partitionKey, policy));
        });
        return this;
    }

    /// <summary>Política por IP del cliente; <paramref name="keyPrefix"/> es el prefijo de la partición.</summary>
    public KnockRateLimitPolicies PerClient(string name, string keyPrefix, int permitLimit, TimeSpan window)
    {
        var policy = new RateLimitPolicySettings { PermitLimit = permitLimit, Window = window };
        _options.AddPolicy(name, context =>
            RateLimitPartition.Get($"{keyPrefix}:{RequestKeys.ClientKey(context)}", partition => new SharedRateLimiter(_services, _serviceName, partition, policy)));
        return this;
    }
}

public static class RateLimitingExtensions
{
    private const string _healthPath = "/health";
    private const string _readyPath = "/health/ready";

    /// <summary>
    /// Límite por IP con el limitador del framework: política general (cualquier ruta salvo <c>/health</c> y <c>/health/ready</c>), la estricta de <c>/auth</c> si el
    /// servicio la declara y la propia de <c>/health/ready</c>; <c>/health</c> es la única exención. Usa Redis compartido y respaldo en memoria por copia.
    /// Responde 429 con <c>Retry-After</c>. Conecta Redis al arrancar para que la primera petición no pague la conexión.
    /// </summary>
    public static IServiceCollection AddKnockRateLimiting(this IServiceCollection services, KnockRateLimitingOptions settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);
        services.AddOptions<RateLimitSettings>()
            .BindConfiguration(RateLimitSettings.SectionName)
            .Validate(
                s => s.Global.IsValid && s.Ready.IsValid && (!settings.StrictAuth || s.Auth.IsValid),
                settings.StrictAuth
                    ? "RateLimiting:Global, RateLimiting:Auth and RateLimiting:Ready need a PermitLimit > 0 and a Window > 0."
                    : "RateLimiting:Global and RateLimiting:Ready need a PermitLimit > 0 and a Window > 0.")
            .ValidateOnStart();
        services.AddRateLimiter(_ => { });
        services.AddHostedService<RedisConnectionWarmup>();
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitSettings>, IServiceProvider>((options, values, provider) =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            settings.ConfigurePolicies?.Invoke(new KnockRateLimitPolicies(options, provider, settings.ServiceName));
            options.GlobalLimiter = BuildGlobalLimiter(values.Value, settings, provider);
            options.OnRejected = async (context, _) =>
            {
                var fallback = WindowOf(context.HttpContext.Request.Path, values.Value, settings);
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var hint) ? hint : fallback;
                var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                await settings.WriteTooManyRequestsAsync(context.HttpContext);
            };
        });
        return services;
    }

    private static PartitionedRateLimiter<HttpContext> BuildGlobalLimiter(RateLimitSettings values, KnockRateLimitingOptions settings, IServiceProvider services)
    {
        var general = PartitionedRateLimiter.Create<HttpContext, string>(context => GlobalPartition(context, values, settings, services));
        if (!settings.StrictAuth)
        {
            return general;
        }

        return PartitionedRateLimiter.CreateChained(
            general,
            PartitionedRateLimiter.Create<HttpContext, string>(context =>
                IsStrictPath(context.Request.Path, settings)
                    ? SharedPartition($"auth:{RequestKeys.ClientKey(context)}", values.Auth, settings, services)
                    : RateLimitPartition.GetNoLimiter("outside-auth")));
    }

    private static RateLimitPartition<string> GlobalPartition(HttpContext context, RateLimitSettings values, KnockRateLimitingOptions settings, IServiceProvider services)
    {
        var path = context.Request.Path;
        if (path.Equals(_healthPath, StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetNoLimiter("health");
        }

        var ready = IsReadyPath(path);
        var key = $"{(ready ? "ready" : "global")}:{RequestKeys.ClientKey(context)}";
        var policy = ready ? values.Ready : values.Global;
        return settings.SharedGlobalCounters
            ? SharedPartition(key, policy, settings, services)
            : RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = policy.PermitLimit,
                Window = policy.Window,
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    }

    private static RateLimitPartition<string> SharedPartition(string key, RateLimitPolicySettings policy, KnockRateLimitingOptions settings, IServiceProvider services) =>
        RateLimitPartition.Get(key, partition => new SharedRateLimiter(services, settings.ServiceName, partition, policy));

    private static TimeSpan WindowOf(PathString path, RateLimitSettings values, KnockRateLimitingOptions settings)
    {
        if (settings.StrictAuth && IsStrictPath(path, settings))
        {
            return values.Auth.Window;
        }

        return IsReadyPath(path) ? values.Ready.Window : values.Global.Window;
    }

    private static bool IsReadyPath(PathString path) => path.Equals(_readyPath, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <c>/api/v{version}/auth</c> y lo que cuelga de ahí, más las rutas extra del servicio. Toda la comparación ignora mayúsculas porque el router de ASP.NET Core
    /// no las distingue: <c>/api/V1/AUTH/firebase</c> llega al mismo endpoint y debe contar para el límite estricto.
    /// </summary>
    private static bool IsStrictPath(PathString path, KnockRateLimitingOptions settings) =>
        RequestKeys.IsApiPath(path, "auth")
        || (settings.IsExtraStrictPath is { } extra && IsApiShape(path) && extra(path.Value!.Split('/', StringSplitOptions.RemoveEmptyEntries)));

    private static bool IsApiShape(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return segments.Length >= 3 && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase) && segments[1][0] is 'v' or 'V';
    }
}

/// <summary>
/// Abre la conexión a Redis al arrancar (si el servicio la registró) para que la primera petición no pague el tiempo de conexión dentro del limitador.
/// Con <c>AbortOnConnectFail=false</c> un Redis caído no impide arrancar: el limitador cae al respaldo por copia y deja el aviso 247.
/// </summary>
internal sealed class RedisConnectionWarmup(IServiceProvider services) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = services.GetService<IConnectionMultiplexer>();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
