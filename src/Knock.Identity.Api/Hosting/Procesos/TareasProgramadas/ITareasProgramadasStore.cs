// jscpd:ignore-start -- EXC-015: copia de src/Knock.Market.Api/Common/TareasProgramadas (spec 028 R-09); se unifica en la biblioteca técnica (KNOCK-251).
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;

internal sealed record TareaReservada(ObjectId Id, string Tipo, string ClaveNegocio, string ReservaId, int Intentos);

/// <summary>Contrato de un manejador de negocio; se registra por <see cref="Tipo"/> (catálogo cerrado). Debe ser idempotente (spec 054, R-09).</summary>
internal interface IManejadorDeTarea
{
    string Tipo { get; }
    Task HandleAsync(TareaReservada task, CancellationToken cancellationToken);
}

internal interface ITareasProgramadasStore
{
    /// <summary>Crea la tarea si no existe; si ya existe con ese <c>tipo</c> y <c>claveNegocio</c> no cambia nada. Devuelve si la creó.</summary>
    Task<bool> ScheduleAsync(IClientSessionHandle? session, string type, string businessKey, DateTime due, DateTime now, CancellationToken cancellationToken);
    Task<TareaReservada?> ReserveAsync(DateTime now, IReadOnlyCollection<string> excludedTypes, CancellationToken cancellationToken);
    Task<bool> CompleteAsync(TareaReservada task, DateTime now, CancellationToken cancellationToken);
    Task<bool> FailAsync(TareaReservada task, DateTime now, CancellationToken cancellationToken);

    /// <summary>Deja la tarea <c>fallida</c> sin más intentos (tipo fuera del catálogo).</summary>
    Task<bool> AbortAsync(TareaReservada task, DateTime now, CancellationToken cancellationToken);
    Task<bool> ConfirmLeaseAsync(IClientSessionHandle session, TareaReservada task, DateTime now, CancellationToken cancellationToken);
}
// jscpd:ignore-end
