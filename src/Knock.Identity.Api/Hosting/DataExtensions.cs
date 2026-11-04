namespace Knock.Identity.Api.Hosting;

/// <summary>
/// Acceso a datos del servicio (spec 003): MongoDB con su base propia y Redis para las sesiones canceladas por <c>sid</c> (spec 016). Solo la infraestructura (cliente, configuración y readiness); la lógica
/// que la usa es de otras specs.
/// </summary>
internal static class DataExtensions
{
    /// <summary>Clave de la base del servicio (<c>Mongo:Databases:Identity</c>).</summary>
    public const string DatabaseKey = "Identity";

    public static IServiceCollection AddKnockData(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddKnockMongoDatabase(DatabaseKey);
        services.AddKnockRedis();
        return services;
    }
}
