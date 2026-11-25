using Knock.Messaging.Api.Hosting;
using Knock.Messaging.Api.Modules.Avisos.Push;
using Knock.Messaging.Api.Modules.Avisos.Services;
using MongoDB.Driver;

namespace Knock.Messaging.Api.Modules.Avisos.Data;

/// <summary>Registra los almacenes de MongoDB del módulo Avisos (solo la capa de datos toca el cliente, spec 003).</summary>
internal static class AvisosDataExtensions
{
    public static IServiceCollection AddAvisosData(this IServiceCollection services)
    {
        services.AddScoped<DeviceStoreFactory>();
        services.AddSingleton<IEnviadosStore>(provider => new EnviadosStore(provider.GetRequiredKeyedService<IMongoDatabase>(DataExtensions.DatabaseKey)));
        services.AddSingleton<IDeviceTokens>(provider => new DeviceTokens(
            provider.GetRequiredKeyedService<IMongoDatabase>(DataExtensions.DatabaseKey),
            provider.GetRequiredService<DeviceKeyConfiguration>(),
            provider.GetRequiredService<ILogger<DeviceTokens>>()));
        services.AddSingleton<IAvisosEnApp>(provider => new AvisosEnAppStore(provider.GetRequiredKeyedService<IMongoDatabase>(DataExtensions.DatabaseKey)));
        services.AddSingleton<IPresenceReader, RedisPresenceReader>();
        return services;
    }
}
