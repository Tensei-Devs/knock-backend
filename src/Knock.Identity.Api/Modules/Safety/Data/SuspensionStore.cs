using Knock.Identity.Api.Hosting.Procesos.Outbox;
using Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;
using Knock.Identity.Api.Modules.Panel;
using MongoDB.Driver;

namespace Knock.Identity.Api.Modules.Safety.Data;

/// <summary>Lo que <c>AccountSuspension</c> necesita de MongoDB: abrir la transacción y dejar eventos en la bandeja de salida dentro de ella. Existe para probar el servicio sin MongoDB.</summary>
internal interface ISuspensionStore
{
    /// <summary>Corre <paramref name="work"/> en una sola transacción de MongoDB: si lanza no queda nada (spec 059, R-04).</summary>
    Task<T> RunAsync<T>(Func<StoreTransaction, Task<T>> work, CancellationToken cancellationToken);

    /// <summary>Escribe los eventos en la bandeja dentro de la transacción; repetir un <c>_id</c> no duplica (spec 054, R-02).</summary>
    Task AddEventsAsync(StoreTransaction transaction, IReadOnlyList<EventoDeOutbox> events, DateTime now, CancellationToken cancellationToken);

    /// <summary>Programa una tarea (spec 054) dentro de la transacción; repetir tipo y clave no duplica (spec 060, R-06a).</summary>
    Task ScheduleTaskAsync(StoreTransaction transaction, string type, string businessKey, DateTime due, DateTime now, CancellationToken cancellationToken);
}

/// <summary>Transacción y bandeja de salida de la suspensión. No es dueño de ninguna colección de negocio: las cuentas, las sesiones y la auditoría se tocan por las interfaces de sus módulos.</summary>
internal sealed class SuspensionStore(IMongoClient client, IOutboxStore outbox, ITareasProgramadasStore tasks) : ISuspensionStore
{
    public Task ScheduleTaskAsync(StoreTransaction transaction, string type, string businessKey, DateTime due, DateTime now, CancellationToken cancellationToken) =>
        tasks.ScheduleAsync((IClientSessionHandle)transaction.Handle, type, businessKey, due, now, cancellationToken);

    public async Task<T> RunAsync<T>(Func<StoreTransaction, Task<T>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        using var session = await client.StartSessionAsync(cancellationToken: cancellationToken);
        return await session.WithTransactionAsync(async (transaction, _) => await work(new StoreTransaction(transaction)), cancellationToken: cancellationToken);
    }

    public async Task AddEventsAsync(StoreTransaction transaction, IReadOnlyList<EventoDeOutbox> events, DateTime now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (var evento in events)
        {
            await outbox.AddAsync((IClientSessionHandle)transaction.Handle, evento, now, cancellationToken);
        }
    }
}
