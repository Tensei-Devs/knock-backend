using System.Globalization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Knock.Identity.Api.Hosting;

// jscpd:ignore-start -- EXC-008: copia de src/Knock.Market.Api/Hosting/RedisExtensions.cs (spec 003); se unifica en una biblioteca técnica.

/// <summary>
/// Conexión a Redis del proceso (sección <c>Redis</c> de appsettings, sin secretos). La cadena llega por
/// <c>ConnectionStrings:KnockRedis</c> desde variables de entorno o user-secrets; en Azure, Key Vault. Spec 003, R-07: un
/// contenedor compartido, con contraseña y sin persistencia.
/// </summary>
internal sealed class KnockRedisOptions
{
    public const string ConnectionStringName = "KnockRedis";
    public const string ConnectionStringKey = "ConnectionStrings:" + ConnectionStringName;
    public const string DatabaseKey = "Redis:Database";
    public const string ConnectTimeoutKey = "Redis:ConnectTimeoutSeconds";
    public const string SyncTimeoutKey = "Redis:SyncTimeoutSeconds";
    public const int DatabaseLimit = 15;
    public const int TimeoutLimitSeconds = 60;
    public const int DefaultDatabase = 0;
    public const int DefaultConnectTimeoutSeconds = 5;
    public const int DefaultSyncTimeoutSeconds = 5;

    public string? ConnectionString { get; set; }

    /// <summary>Base lógica de Redis (0 a 15).</summary>
    public int? Database { get; set; } = DefaultDatabase;

    public int? ConnectTimeoutSeconds { get; set; } = DefaultConnectTimeoutSeconds;

    public int? SyncTimeoutSeconds { get; set; } = DefaultSyncTimeoutSeconds;
}

/// <summary>Valida la conexión al arrancar. Los mensajes nombran la clave, nunca el valor (la cadena trae la contraseña).</summary>
internal sealed class KnockRedisOptionsValidator : IValidateOptions<KnockRedisOptions>
{
    public ValidateOptionsResult Validate(string? name, KnockRedisOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            failures.Add($"{KnockRedisOptions.ConnectionStringKey} is not configured.");
        }
        else if (!TryParse(options.ConnectionString, out var connection))
        {
            failures.Add($"{KnockRedisOptions.ConnectionStringKey} is not a valid Redis connection string.");
        }
        else if (string.IsNullOrWhiteSpace(connection!.Password))
        {
            failures.Add($"{KnockRedisOptions.ConnectionStringKey} must include a password (spec 003, R-07).");
        }

        if (options.Database is not (>= 0 and <= KnockRedisOptions.DatabaseLimit))
        {
            failures.Add($"{KnockRedisOptions.DatabaseKey} must be an integer from 0 to {KnockRedisOptions.DatabaseLimit}.");
        }

        ValidateTimeout(failures, options.ConnectTimeoutSeconds, KnockRedisOptions.ConnectTimeoutKey);
        ValidateTimeout(failures, options.SyncTimeoutSeconds, KnockRedisOptions.SyncTimeoutKey);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateTimeout(List<string> failures, int? seconds, string key)
    {
        if (seconds is not (>= 1 and <= KnockRedisOptions.TimeoutLimitSeconds))
        {
            failures.Add($"{key} must be an integer from 1 to {KnockRedisOptions.TimeoutLimitSeconds} (seconds).");
        }
    }

    private static bool TryParse(string connectionString, out ConfigurationOptions? options)
    {
        try
        {
            options = ConfigurationOptions.Parse(connectionString);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            // Sin el mensaje: el del cliente puede repetir la cadena.
            options = null;
            return false;
        }
    }
}

internal static class RedisExtensions
{
    public const string HealthCheckName = "redis";

    /// <summary>Tiempo máximo del PING de readiness; los tiempos del cliente se configuran aparte.</summary>
    public static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Un solo <see cref="IConnectionMultiplexer"/> por proceso, validado al arrancar, y una comprobación de readiness. Llamarlo más de
    /// una vez no registra nada nuevo. Lo llama el módulo que use Redis, y solo su capa <c>Data</c> lo usa (spec 003, R-04); sin
    /// ningún módulo que lo pida, la API arranca sin Redis.
    /// </summary>
    public static IServiceCollection AddKnockRedis(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IConnectionMultiplexer)))
        {
            return services;
        }

        services.AddOptions<KnockRedisOptions>()
            .Configure<IConfiguration>(Bind)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<KnockRedisOptions>, KnockRedisOptionsValidator>();
        services.AddSingleton<IConnectionMultiplexer>(provider =>
            ConnectionMultiplexer.Connect(CreateConfiguration(provider.GetRequiredService<IOptions<KnockRedisOptions>>().Value)));
        services.AddHealthChecks().AddCheck<RedisReadinessHealthCheck>(HealthCheckName, tags: [HealthExtensions.ReadyTag]);
        return services;
    }

    internal static ConfigurationOptions CreateConfiguration(KnockRedisOptions options)
    {
        var configuration = ConfigurationOptions.Parse(options.ConnectionString
            ?? throw new InvalidOperationException($"{KnockRedisOptions.ConnectionStringKey} is not configured."));
        configuration.AbortOnConnectFail = false;
        configuration.ClientName = "knock-identity";
        configuration.ConnectTimeout = Milliseconds(options.ConnectTimeoutSeconds, KnockRedisOptions.ConnectTimeoutKey);
        configuration.SyncTimeout = Milliseconds(options.SyncTimeoutSeconds, KnockRedisOptions.SyncTimeoutKey);
        configuration.DefaultDatabase = options.Database
            ?? throw new InvalidOperationException($"{KnockRedisOptions.DatabaseKey} is not configured.");
        return configuration;
    }

    private static int Milliseconds(int? value, string key) =>
        checked((int)TimeSpan.FromSeconds(value ?? throw new InvalidOperationException($"{key} is not configured.")).TotalMilliseconds);

    private static void Bind(KnockRedisOptions options, IConfiguration configuration)
    {
        options.ConnectionString = configuration.GetConnectionString(KnockRedisOptions.ConnectionStringName);
        options.Database = ParseOrDefault(configuration, KnockRedisOptions.DatabaseKey, KnockRedisOptions.DefaultDatabase);
        options.ConnectTimeoutSeconds = ParseOrDefault(
            configuration, KnockRedisOptions.ConnectTimeoutKey, KnockRedisOptions.DefaultConnectTimeoutSeconds);
        options.SyncTimeoutSeconds = ParseOrDefault(
            configuration, KnockRedisOptions.SyncTimeoutKey, KnockRedisOptions.DefaultSyncTimeoutSeconds);
    }

    private static int? ParseOrDefault(IConfiguration configuration, string key, int defaultValue)
    {
        var value = configuration[key];
        return value is null ? defaultValue : ParseInteger(value);
    }

    private static int? ParseInteger(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;
}

/// <summary>PING a Redis con la etiqueta <c>ready</c>; una sola por proceso.</summary>
internal sealed class RedisReadinessHealthCheck(IConnectionMultiplexer connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RedisExtensions.PingTimeout);
        try
        {
            await connection.GetDatabase().PingAsync().WaitAsync(timeout.Token);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Solo el tipo: el mensaje puede traer el host. La cancelación que pide quien llama sí se propaga.
            return HealthCheckResult.Unhealthy($"Redis did not respond ({ex.GetType().Name}).");
        }
    }
}
// jscpd:ignore-end -- EXC-008
