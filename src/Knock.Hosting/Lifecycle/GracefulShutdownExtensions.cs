using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Knock.Hosting.Lifecycle;

/// <summary>
/// Apagado ordenado (spec 034, R-03): al recibir la señal de apagado <c>/health/ready</c> responde 503 de inmediato (la réplica sale de rotación)
/// y el host espera hasta <see cref="ShutdownTimeoutSeconds"/> a que terminen las peticiones en curso; el periodo de gracia del contenedor es de 30 s.
/// </summary>
public static class GracefulShutdownExtensions
{
    /// <summary>Etiqueta con la que los servicios registran sus comprobaciones de disponibilidad (<c>/health/ready</c>).</summary>
    public const string ReadyTag = "ready";

    public const int ShutdownTimeoutSeconds = 25;

    public static IServiceCollection AddKnockGracefulShutdown(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(ShutdownTimeoutSeconds));
        services.AddHealthChecks().AddCheck<ShuttingDownHealthCheck>("apagado", tags: [ReadyTag]);
        return services;
    }
}

/// <summary>Falla (503) desde que el host empieza a apagarse; antes de eso es saludable.</summary>
internal sealed class ShuttingDownHealthCheck(IHostApplicationLifetime lifetime) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(lifetime.ApplicationStopping.IsCancellationRequested
            ? HealthCheckResult.Unhealthy("shutting down")
            : HealthCheckResult.Healthy());
}
