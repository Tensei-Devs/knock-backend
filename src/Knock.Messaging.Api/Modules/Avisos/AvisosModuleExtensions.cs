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
        services.AddPushSender();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<DespachadorDeAvisos>();
        return services;
    }

    /// <summary>Con <c>Notificaciones:PushHabilitado=false</c> (por omisión) queda el <see cref="NoOpPushSender"/>; encendido, exige proyecto y cuenta de servicio de FCM o no arranca (falla cerrado).</summary>
    internal static IServiceCollection AddPushSender(this IServiceCollection services)
    {
        services.AddSingleton<IPushSender>(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            if (!PushOptions.Read(configuration).Enabled)
            {
                return new NoOpPushSender();
            }

            var options = FcmOptions.Read(configuration);
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var token = new FcmAccessToken(http, options, TimeProvider.System);
            return new FcmPushSender(http, options, token, provider.GetRequiredService<ILogger<FcmPushSender>>());
        });
        return services;
    }
}
