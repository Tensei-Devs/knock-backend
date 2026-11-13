using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Knock.Messaging.Api.Hosting;

// jscpd:ignore-start -- EXC-008: igual en Identity, Messaging y Gateway (spec 003); se unifica en una biblioteca técnica.
/// <summary>
/// Readiness de las dependencias de datos del servicio (spec 003): cada una registra su comprobación con <see cref="ReadyTag"/> y
/// <c>/health/ready</c> responde con ellas. La ruta <c>/health</c> de vida es de la spec 001.
/// </summary>
internal static class HealthExtensions
{
    public const string ReadyTag = "ready";

    public const string ReadyPath = "/health/ready";

    public static IEndpointRouteBuilder MapKnockReadiness(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapHealthChecks(ReadyPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) })
            .AllowAnonymous();
        return endpoints;
    }
}
// jscpd:ignore-end -- EXC-008
