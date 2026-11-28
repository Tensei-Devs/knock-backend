namespace Knock.Hosting.Concurrency;

/// <summary>
/// Turno por clave dentro de una instancia: las escrituras que compiten por el mismo índice único se ejecutan una tras otra en vez de a la vez. Causa raíz de los 500 intermitentes bajo
/// concurrencia: varios <c>insert</c> o <c>upsert</c> simultáneos sobre la misma clave única se bloquean entre sí en documentdb-local («Could not acquire lock ... due to deadlock», código 24)
/// y agotan sus reintentos esperando el candado del otro. La clave se reparte en un número fijo de franjas por hash (sin crecer con las claves); dos claves distintas que caigan en la misma
/// franja solo esperan un poco. Entre instancias siguen valiendo el índice único, la relectura y el reintento acotado de cada almacén.
/// </summary>
public static class KeyedGate
{
    private const int _stripes = 64;

    private static readonly SemaphoreSlim[] _gates = [.. Enumerable.Range(0, _stripes).Select(_ => new SemaphoreSlim(1, 1))];

    /// <summary>Ejecuta <paramref name="work"/> cuando le toca el turno de <paramref name="key"/> y libera el turno al terminar, falle o no.</summary>
    public static async Task<T> RunAsync<T>(string key, Func<Task<T>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(work);
        var gate = _gates[(uint)StringComparer.Ordinal.GetHashCode(key) % _stripes];
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await work();
        }
        finally
        {
            gate.Release();
        }
    }
}
