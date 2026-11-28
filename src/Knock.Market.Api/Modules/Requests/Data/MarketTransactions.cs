using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Requests.Data;

/// <summary>Transacciones de MongoDB del módulo Requests con el reintento de conflictos de escritura que comparten sus almacenes.</summary>
internal static class MarketTransactions
{
    private const int _maxConflictAttempts = 5;

    /// <summary>
    /// Corre la transacción y reintenta los conflictos de escritura entre peticiones simultáneas (código 112 o la etiqueta <c>TransientTransactionError</c>) hasta cinco veces con una espera
    /// corta. Si el conflicto no se resuelve, devuelve <paramref name="conflictResult"/> (la petición «no se aplicó») y el llamador explica el recurso con su estado actual: un conflicto
    /// nunca sale como 500.
    /// </summary>
    public static async Task<T> RunAsync<T>(IMongoDatabase database, Func<IClientSessionHandle, CancellationToken, Task<T>> body, T conflictResult, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(body);
        for (var attempt = 1; attempt <= _maxConflictAttempts; attempt++)
        {
            using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
            try
            {
                return await session.WithTransactionAsync(body, cancellationToken: cancellationToken);
            }
            catch (MongoException exception) when (IsWriteConflict(exception))
            {
                await Task.Delay(10 * attempt, cancellationToken);
            }
        }

        return conflictResult;
    }

    private static bool IsWriteConflict(MongoException exception) =>
        exception.HasErrorLabel("TransientTransactionError")
        || exception is MongoCommandException { Code: 112 }
        || exception is MongoWriteException { WriteError.Code: 112 };
}
