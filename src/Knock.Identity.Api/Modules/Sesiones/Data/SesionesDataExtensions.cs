using Knock.Identity.Api.Hosting;

namespace Knock.Identity.Api.Modules.Sesiones.Data;

/// <summary>Registro de la capa de datos de Sesiones: la base del servicio del cliente compartido de Mongo y su acceso a las colecciones.</summary>
internal static class SesionesDataExtensions
{
    /// <summary>Nombre del módulo y clave de su base (<c>Mongo:Databases:Identity</c>).</summary>
    public const string ModuleKey = "Identity";

    public static IServiceCollection AddSesionesData(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddKnockMongoDatabase(ModuleKey);
        services.AddSingleton<SesionesStore>();
        services.AddSingleton<ISesionesStore>(provider => provider.GetRequiredService<SesionesStore>());
        return services;
    }
}
