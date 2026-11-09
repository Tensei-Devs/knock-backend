namespace Knock.Messaging.Api.Hosting;

/// <summary>
/// Acceso a datos del servicio (spec 003): MongoDB con su base propia; por ahora no usa Redis (decisión del líder, 2026-11-04). Solo la infraestructura (cliente, configuración y readiness); la lógica
/// que la usa es de otras specs.
/// </summary>
internal static class DataExtensions
{
    /// <summary>Clave de la base del servicio (<c>Mongo:Databases:Messaging</c>).</summary>
    public const string DatabaseKey = "Messaging";

    public static IServiceCollection AddKnockData(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddKnockMongoDatabase(DatabaseKey);
        return services;
    }
}
