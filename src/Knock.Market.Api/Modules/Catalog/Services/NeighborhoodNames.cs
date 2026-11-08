using Knock.Market.Api.Modules.Catalog.Data;
using Knock.Market.Api.Modules.Catalog.Domain;

namespace Knock.Market.Api.Modules.Catalog.Services;

internal sealed class NeighborhoodNames(CatalogStore store) : INeighborhoodNames
{
    public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return store.ColoniaNameExistsAsync(SearchNameNormalizer.Normalize(name), cancellationToken);
    }
}
