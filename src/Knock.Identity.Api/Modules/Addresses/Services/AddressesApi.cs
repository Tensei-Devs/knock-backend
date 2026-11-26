using Knock.Identity.Api.Modules.Addresses.Data;
using MongoDB.Bson;

namespace Knock.Identity.Api.Modules.Addresses.Services;

internal sealed class AddressesApi(IAddressesStore store, AddressCipher cipher) : IAddressesApi
{
    public string ModuleName => "Addresses";

    public async Task<IReadOnlyList<AddressExport>> ExportAsync(string accountId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(accountId, out var owner))
        {
            return [];
        }

        var stored = await store.ListAsync(owner, cancellationToken);
        return [.. stored.Select(cipher.Open).Select(address => new AddressExport(address.Street, address.Colonia, address.References, address.Principal, address.CreatedAt))];
    }
}
