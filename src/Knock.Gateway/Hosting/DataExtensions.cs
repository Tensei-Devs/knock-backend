namespace Knock.Gateway.Hosting;

/// <summary>
/// Acceso a datos del servicio (spec 003): Redis para leer las sesiones canceladas y llevar el límite de peticiones (spec 009); no usa MongoDB. Solo la infraestructura (cliente, configuración y readiness); la lógica
/// que la usa es de otras specs.
/// </summary>
internal static class DataExtensions
{
    public static IServiceCollection AddKnockData(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddKnockRedis();
        return services;
    }
}
