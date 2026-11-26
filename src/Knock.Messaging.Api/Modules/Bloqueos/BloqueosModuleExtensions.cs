using Knock.Messaging.Api.Modules.Bloqueos.Data;
using Knock.Messaging.Api.Modules.Bloqueos.Services;

namespace Knock.Messaging.Api.Modules.Bloqueos;

/// <summary>Registro del módulo Bloqueos de Mensajería (spec 043, R-23): la proyección mínima <c>bloqueos</c> que alimentan los eventos de Identidad.</summary>
public static class BloqueosModuleExtensions
{
    public static IServiceCollection AddBloqueosModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IBloqueosProyeccion, BloqueosProyeccion>();
        services.AddSingleton<IBloqueosApi>(provider => provider.GetRequiredService<IBloqueosProyeccion>());
        services.AddSingleton<BloqueosService>();
        return services;
    }
}
