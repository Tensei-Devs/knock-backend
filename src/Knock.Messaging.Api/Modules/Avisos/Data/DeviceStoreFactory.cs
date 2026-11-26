using Knock.Messaging.Api.Hosting;
using Knock.Messaging.Api.Modules.Avisos.Services;
using MongoDB.Driver;

namespace Knock.Messaging.Api.Modules.Avisos.Data;

/// <summary>Crea el <see cref="DeviceStore"/> solo si las escrituras están habilitadas; así el servicio no toca MongoDB (spec 003, R-04).</summary>
internal sealed class DeviceStoreFactory(IServiceProvider services, DeviceKeyConfiguration keys)
{
    public bool Available => keys.WritesEnabled;

    public DeviceStore Create()
    {
        if (!Available)
        {
            throw new InvalidOperationException("Device service is unavailable.");
        }

        return new DeviceStore(services.GetRequiredKeyedService<IMongoDatabase>(DataExtensions.DatabaseKey), keys.Protection!);
    }
}
