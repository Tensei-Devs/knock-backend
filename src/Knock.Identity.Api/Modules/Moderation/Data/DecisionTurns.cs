namespace Knock.Identity.Api.Modules.Moderation.Data;

/// <summary>
/// Causa raíz de 500 en decisiones simultáneas (spec 053, REP-05 y REP-03; misma técnica que KNOCK-295): diez transacciones sobre el mismo documento se bloquean entre sí en documentdb-local
/// («Could not acquire lock ... due to deadlock») y agotan el tiempo antes de que alguien suelte el candado. Dentro de la instancia, las decisiones sobre un mismo elemento se turnan (candado por franja
/// del hash del id) y quien entra después relee el estado: si ya no está pendiente responde «ya decidido» (409) sin abrir transacción. Entre instancias sigue valiendo el filtro por estado de la transacción.
/// </summary>
internal static class DecisionTurns
{
    private static readonly SemaphoreSlim[] _gates = [.. Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1))];

    /// <param name="id">Identificador del elemento a decidir.</param>
    /// <param name="settled">Relee el estado: <see langword="null"/> si sigue pendiente; si no, <see cref="DecisionStoreOutcome.AlreadyDecided"/> o <see cref="DecisionStoreOutcome.NotFound"/>.</param>
    /// <param name="map">Convierte el desenlace ya resuelto en el resultado del almacén.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <param name="decide">La transacción de la decisión; solo corre si <paramref name="settled"/> devolvió <see langword="null"/>.</param>
    public static async Task<T> RunAsync<T>(string id, Func<Task<DecisionStoreOutcome?>> settled, Func<DecisionStoreOutcome, T> map, Func<Task<T>> decide, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        var gate = _gates[(uint)StringComparer.Ordinal.GetHashCode(id) % _gates.Length];
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await settled() is { } outcome ? map(outcome) : await decide();
        }
        finally
        {
            gate.Release();
        }
    }
}
