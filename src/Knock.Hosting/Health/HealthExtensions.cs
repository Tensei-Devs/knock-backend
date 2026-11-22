using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Knock.Hosting.Health;

/// <summary>
/// Readiness de las dependencias de datos del servicio (spec 003): cada una registra su comprobación con <see cref="ReadyTag"/> y
/// <c>/health/ready</c> responde con ellas. La ruta <c>/health</c> de vida es de la spec 001 y no genera spans (TT-OBS-01).
/// </summary>
public static class HealthExtensions
{
    public const string ReadyTag = "ready";

    /// <summary>Raíz de las rutas de salud (<c>/health</c> y <c>/health/ready</c>).</summary>
    public const string LivenessPath = "/health";

    public const string ReadyPath = "/health/ready";

    /// <summary>Las comprobaciones de readiness las registra cada dependencia: MongoDB (<c>AddKnockMongo</c>), Redis y Blob (Media).</summary>
    public static IServiceCollection AddKnockHealth(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHealthChecks();
        return services;
    }

    public static IEndpointRouteBuilder MapKnockReadiness(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapHealthChecks(ReadyPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) });
        return endpoints;
    }

    /// <summary>La liveness (<c>/health</c>, sin comprobaciones) y la readiness, juntas.</summary>
    public static IEndpointRouteBuilder MapKnockHealth(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapHealthChecks(LivenessPath, new HealthCheckOptions { Predicate = _ => false });
        return endpoints.MapKnockReadiness();
    }
}
