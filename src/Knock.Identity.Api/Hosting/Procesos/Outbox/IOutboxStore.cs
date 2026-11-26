using MongoDB.Driver;

namespace Knock.Identity.Api.Hosting.Procesos.Outbox;

internal interface IOutboxStore
{
    /// <summary>Escribe el evento en la misma transacción que el cambio que lo provoca (R-02). Si ya existe su <c>_id</c> no cambia nada. Devuelve si lo creó.</summary>
    Task<bool> AddAsync(IClientSessionHandle session, EventoDeOutbox evento, DateTime now, CancellationToken cancellationToken);

    /// <summary>Reserva un evento vencido con un solo <c>findOneAndUpdate</c> (R-03).</summary>
    Task<OutboxReservado?> ReserveAsync(DateTime now, CancellationToken cancellationToken);
    Task<bool> MarkDeliveredAsync(OutboxReservado evento, DateTime now, CancellationToken cancellationToken);

    /// <summary>Suma un intento y reprograma, o deja <c>fallido</c> al quinto fallo o si <paramref name="permanent"/> (R-04).</summary>
    Task<bool> FailAsync(OutboxReservado evento, bool permanent, DateTime now, CancellationToken cancellationToken);
}

/// <summary>Entrega un evento a su servicio destino con el token interno de la spec 037 (R-15). La implementación HTTP llega con esa spec.</summary>
internal interface IEntregaInterna
{
    Task<ResultadoDeEntrega> DeliverAsync(OutboxReservado evento, CancellationToken cancellationToken);
}

/// <summary>Valor por omisión mientras no exista la entrega interna de la spec 037: cada evento queda para reintento y nunca se marca entregado.</summary>
internal sealed class EntregaInternaNoDisponible : IEntregaInterna
{
    public Task<ResultadoDeEntrega> DeliverAsync(OutboxReservado evento, CancellationToken cancellationToken) =>
        Task.FromResult(ResultadoDeEntrega.Reintentable);
}
