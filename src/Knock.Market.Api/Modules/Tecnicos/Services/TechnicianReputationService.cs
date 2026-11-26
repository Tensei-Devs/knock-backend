using Knock.Market.Api.Modules.Tecnicos.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace Knock.Market.Api.Modules.Tecnicos.Services;

internal interface IReputationReader
{
    /// <summary>Reputación del perfil de técnico de la cuenta, o <see langword="null"/> si la cuenta no es técnico.</summary>
    Task<ReputationResponse?> FindOwnAsync(string sub, CancellationToken cancellationToken);
}

/// <summary>
/// Reputación propia (spec 046, TEC-13, R-34, R-35 y R-48): lee el documento <c>reputacion</c> que la spec 051 deja calculado en el perfil. Se guarda 1 minuto por
/// técnico en la caché de la réplica; la clave lleva solo el <c>sub</c> de quien consulta, nunca el de otra persona. Una cuenta sin perfil de técnico recibe
/// <see langword="null"/> (404).
/// </summary>
internal sealed class TechnicianReputationService(IReputationReader reader, IMemoryCache cache)
{
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(1);

    public static string CacheKey(string sub) => $"tecnicos:reputacion:{sub}";

    public async Task<ReputationResponse?> GetOwnAsync(string sub, CancellationToken cancellationToken)
    {
        var key = CacheKey(sub);
        if (cache.TryGetValue(key, out ReputationResponse? cached) && cached is not null)
        {
            return cached;
        }

        var reputation = await reader.FindOwnAsync(sub, cancellationToken);
        if (reputation is not null)
        {
            cache.Set(key, reputation, CacheLifetime);
        }

        return reputation;
    }

    /// <summary>Quita la reputación de la caché de esta réplica; la llama quien recalcula la reputación (spec 051).</summary>
    public void Forget(string sub) => cache.Remove(CacheKey(sub));
}
