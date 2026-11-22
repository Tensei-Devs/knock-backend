using Knock.Messaging.Api.Modules.Avisos.Data;
using Knock.Messaging.Api.Modules.Avisos.Push;
using Knock.Messaging.Api.Modules.Avisos.Services;

namespace Knock.Messaging.Api.Modules.Avisos;

/// <summary>Registro del módulo Avisos (spec 037): dispositivos, despachador y puertos. FCM va detrás de <see cref="IPushSender"/> y el interruptor <c>Notificaciones:PushHabilitado</c> viene apagado.</summary>
public static class AvisosModuleExtensions
{
    public static IServiceCollection AddAvisosModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(provider => DeviceKeyConfiguration.Read(provider.GetRequiredService<IConfiguration>()));
        services.AddAvisosData();
        services.AddScoped<DispositivosService>();
        services.AddSingleton(provider => PushOptions.Read(provider.GetRequiredService<IConfiguration>()));
        services.AddSingleton<IPushSender, NoOpPushSender>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<DespachadorDeAvisos>();
        return services;
    }
}
