using Knock.Market.Api.Modules.Tecnicos.Data;

namespace Knock.Market.Api.Modules.Tecnicos.Services;

internal sealed class TecnicosApi(ITechnicianProfileStore store, TechnicianAccountStore accountStore) : ITecnicosApi
{
    public string ModuleName => TecnicosModuleExtensions.ModuleKey;

    public Task<bool> HasProfileAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        return store.ExistsAsync(userId, cancellationToken);
    }

    public Task<string?> ExportProfileJsonAsync(string accountSub, CancellationToken cancellationToken) => accountStore.ExportProfileJsonAsync(accountSub, cancellationToken);

    public Task AnonymizeProfileAsync(string accountSub, CancellationToken cancellationToken) => accountStore.AnonymizeProfileAsync(accountSub, cancellationToken);
}
