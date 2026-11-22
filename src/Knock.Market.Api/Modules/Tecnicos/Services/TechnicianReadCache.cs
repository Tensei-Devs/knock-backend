using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace Knock.Market.Api.Modules.Tecnicos.Services;

/// <summary>
/// Señal única de invalidación de las lecturas públicas de técnicos (mapa y Explorar, spec 046 R-13 y R-38). Cada entrada de caché se liga al token vigente; al escribir un
/// perfil se llama <see cref="Invalidate"/> y todas las entradas ligadas caducan a la vez, aunque cada petición use su propia instancia de servicio.
/// </summary>
internal sealed class TechnicianReadCache : IDisposable
{
    private CancellationTokenSource _source = new();

    public void Invalidate()
    {
        var previous = Interlocked.Exchange(ref _source, new CancellationTokenSource());
        previous.Cancel();
        previous.Dispose();
    }

    /// <summary>Guarda <paramref name="value"/> durante <paramref name="lifetime"/> o hasta la siguiente invalidación, lo que ocurra primero.</summary>
    public void Set<T>(IMemoryCache cache, string key, T value, TimeSpan lifetime) =>
        cache.Set(key, value, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = lifetime }.AddExpirationToken(new CancellationChangeToken(_source.Token)));

    public void Dispose() => _source.Dispose();
}
