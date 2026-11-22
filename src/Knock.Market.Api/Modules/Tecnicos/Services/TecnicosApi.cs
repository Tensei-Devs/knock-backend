namespace Knock.Market.Api.Modules.Tecnicos.Services;

internal sealed class TecnicosApi(ITechnicianProfileStore store) : ITecnicosApi
{
    public Task<bool> HasProfileAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        return store.ExistsAsync(userId, cancellationToken);
    }
}
