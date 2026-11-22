using Knock.Market.Api.Modules.Tecnicos.Data;

namespace Knock.Market.Api.Modules.Tecnicos.Services;

internal sealed class TecnicosApi(TechnicianAccountStore store) : ITecnicosApi
{
    public string ModuleName => TecnicosModuleExtensions.ModuleKey;

    public Task<string?> ExportProfileJsonAsync(string accountSub, CancellationToken cancellationToken) => store.ExportProfileJsonAsync(accountSub, cancellationToken);

    public Task AnonymizeProfileAsync(string accountSub, CancellationToken cancellationToken) => store.AnonymizeProfileAsync(accountSub, cancellationToken);
}
