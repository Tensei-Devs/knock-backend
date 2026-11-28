using Knock.Messaging.Api.Modules.Avisos.Data;

namespace Knock.Messaging.Api.Modules.Avisos.Services;

internal sealed class DispositivosService(DeviceStoreFactory stores)
{
    public bool Available => stores.Available;

    public async Task<DeviceRegistration> RegisterAsync(string person, string session, string platform, string token, CancellationToken cancellationToken)
    {
        var store = stores.Create();
        return await store.RegisterAsync(person, session, platform, token, DateTime.UtcNow, cancellationToken);
    }

    public Task<bool> DeleteAsync(string person, string id, CancellationToken cancellationToken) =>
        stores.Create().DeleteAsync(person, id, cancellationToken);
}
