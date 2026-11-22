using Knock.Messaging.Api.Hosting;
using Knock.Messaging.Api.Modules.Avisos.Data;
using MongoDB.Driver;

namespace Knock.Messaging.Api.Modules.Avisos.Services;

internal sealed class DispositivosService(IServiceProvider services, DeviceKeyConfiguration keys)
{
    public bool Available => keys.WritesEnabled;

    public async Task<DeviceRegistration> RegisterAsync(string person, string session, string platform, string token, CancellationToken cancellationToken)
    {
        var store = CreateStore();
        return await store.RegisterAsync(person, session, platform, token, DateTime.UtcNow, cancellationToken);
    }

    public Task<bool> DeleteAsync(string person, string id, CancellationToken cancellationToken) =>
        CreateStore().DeleteAsync(person, id, cancellationToken);

    private DeviceStore CreateStore()
    {
        if (!Available)
        {
            throw new InvalidOperationException("Device service is unavailable.");
        }

        return new DeviceStore(services.GetRequiredKeyedService<IMongoDatabase>(DataExtensions.DatabaseKey), keys.Protection!);
    }
}
