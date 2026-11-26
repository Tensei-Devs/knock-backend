namespace Knock.Market.Api.Modules.Assistant.Data;

/// <summary>Reserva y ajuste atómicos de tokens en Redis (spec 024, R-07 y R-08). Lo implementa <see cref="ConsumoIaStore"/>.</summary>
internal interface IConsumoIa
{
    Task<ReservationResult> ReserveAsync(string user, DateOnly day, int amount, int userLimit, int globalLimit, CancellationToken cancellationToken);

    Task<AdjustmentResult> AdjustAsync(ConsumptionReservation reservation, int actual, CancellationToken cancellationToken);
}

/// <summary>Registro de uso sin contenido en <c>uso_ia</c> (spec 024, R-12). Lo implementa <see cref="UsoIaStore"/>.</summary>
internal interface IUsoIa
{
    Task SaveAsync(UsageRecord usage, CancellationToken cancellationToken);
}
