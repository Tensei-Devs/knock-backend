using Knock.Hosting.Procesos;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace Knock.Market.Api.Common.Bandeja;

/// <summary>Interruptor y ritmo del despachador de la bandeja de salida de Mercado (spec 053 y spec 050, R-32). Apagado por omisión: sin <c>Bandeja:Habilitada</c> los mensajes siguen <c>pendiente</c>.</summary>
internal sealed class BandejaOptions
{
    public const string SectionName = "Bandeja";

    public bool Habilitada { get; set; }

    [System.ComponentModel.DataAnnotations.Range(1, 3600)]
    public int IntervaloSegundos { get; set; } = 5;

    [System.ComponentModel.DataAnnotations.Range(1, 30)]
    public int TimeoutSeconds { get; set; } = 3;
}

/// <summary>Un mensaje de la bandeja reservado por el despachador, con el sello de su arrendamiento. Los <see cref="Datos"/> llevan solo identificadores.</summary>
internal sealed record MensajeReservado(string Id, string Tipo, BsonDocument Datos, string ReservaId, int Intentos);

/// <summary>Resultado de entregar un mensaje: <c>Aceptado</c> (el destino respondió 202), <c>Reintentable</c> (red, 5xx, 429) o <c>Rechazado</c> (401, 403, 422: no se reintenta).</summary>
internal enum ResultadoDeEntrega
{
    Aceptado,
    Reintentable,
    Rechazado,
}

/// <summary>Entrega un tipo de mensaje de la bandeja a su servicio destino. Un tipo sin entrega registrada se queda <c>pendiente</c>.</summary>
internal interface IEntregaDeBandeja
{
    string Tipo { get; }

    Task<ResultadoDeEntrega> DeliverAsync(MensajeReservado mensaje, CancellationToken cancellationToken);
}

internal interface IBandejaStore
{
    /// <summary>Reserva un mensaje vencido de alguno de los <paramref name="types"/> con un solo <c>findOneAndUpdate</c>; dos despachadores nunca toman el mismo.</summary>
    Task<MensajeReservado?> ReserveAsync(DateTime now, IReadOnlyCollection<string> types, CancellationToken cancellationToken);

    Task<bool> MarkDeliveredAsync(MensajeReservado mensaje, DateTime now, CancellationToken cancellationToken);

    /// <summary>Suma un intento y reprograma, o deja <c>fallido</c> al quinto fallo o si <paramref name="permanent"/>.</summary>
    Task<bool> FailAsync(MensajeReservado mensaje, bool permanent, DateTime now, CancellationToken cancellationToken);
}

/// <summary>
/// Despachador de la bandeja de salida de Mercado: reserva un mensaje, lo entrega y lo marca <c>entregado</c> solo si el destino respondió 202 (al menos una vez; el consumidor es idempotente). Reintenta con espera
/// creciente y al quinto fallo lo deja <c>fallido</c>. Los registros llevan tipo e identificador, nunca la carga.
/// </summary>
internal sealed partial class DespachadorDeBandeja(IBandejaStore store, IEnumerable<IEntregaDeBandeja> deliveries, TimeProvider clock, ILogger<DespachadorDeBandeja> logger)
{
    public const int MaxIntentos = 5;
    private static readonly int[] _esperasSegundos = [5, 30, 120, 600];
    private readonly Dictionary<string, IEntregaDeBandeja> _deliveries = deliveries.ToDictionary(x => x.Tipo, StringComparer.Ordinal);

    public static TimeSpan? Espera(int intentos) => intentos is >= 1 and < MaxIntentos ? TimeSpan.FromSeconds(_esperasSegundos[intentos - 1]) : null;

    public async Task<bool> DispatchOneAsync(CancellationToken cancellationToken)
    {
        var types = _deliveries.Keys.ToArray();
        var message = types.Length == 0 ? null : await store.ReserveAsync(clock.GetUtcNow().UtcDateTime, types, cancellationToken);
        if (message is null)
        {
            return false;
        }

        ResultadoDeEntrega result;
        try
        {
            result = await _deliveries[message.Tipo].DeliverAsync(message, cancellationToken);
        }
        catch (Exception error) when (!(error is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            result = ResultadoDeEntrega.Reintentable;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var owned = result == ResultadoDeEntrega.Aceptado
            ? await store.MarkDeliveredAsync(message, now, cancellationToken)
            : await store.FailAsync(message, result == ResultadoDeEntrega.Rechazado, now, cancellationToken);
        var attempt = message.Intentos + 1;
        var lastFailure = result == ResultadoDeEntrega.Rechazado || attempt >= MaxIntentos;
        var outcome = ProcesosMetrics.Outcome(owned, result == ResultadoDeEntrega.Aceptado, lastFailure, ProcesosMetrics.Entregado, ProcesosMetrics.Fallido);
        ProcesosMetrics.RecordAttempt(logger, ProcesosMetrics.Outbox, message.Tipo, message.Id, attempt, outcome);
        if (!owned)
        {
            LeaseLostOnDispatch(logger, message.Tipo, message.Id);
        }
        else if (result != ResultadoDeEntrega.Aceptado && lastFailure)
        {
            DispatchFailed(logger, message.Tipo, message.Id);
        }

        return true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dispatch lease lost: {MessageType} {MessageId}")]
    private static partial void LeaseLostOnDispatch(ILogger logger, string messageType, string messageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "delivery_failed: dispatch {MessageType} {MessageId}")]
    private static partial void DispatchFailed(ILogger logger, string messageType, string messageId);
}

internal sealed class BandejaHostedService(IServiceScopeFactory scopes, IOptions<BandejaOptions> settings, TimeProvider timeProvider, ILogger<BandejaHostedService> log)
    : ProcesoPeriodico(scopes, timeProvider, log, "outbox-dispatch")
{
    protected override int IntervaloSegundos => settings.Value.IntervaloSegundos;

    protected override Task RunStepAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        settings.Value.Habilitada ? services.GetRequiredService<DespachadorDeBandeja>().DispatchOneAsync(cancellationToken) : Task.CompletedTask;
}
