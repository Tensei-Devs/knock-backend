using Knock.Hosting.Procesos;
using Microsoft.Extensions.Options;

namespace Knock.Identity.Api.Hosting.Procesos.Outbox;

internal sealed class OutboxOptions
{
    public int IntervaloSegundos { get; set; } = 5;
}

/// <summary>
/// Procesador de la bandeja de salida de Identidad (spec 054, R-03 a R-05 y R-12). Reserva un evento, lo entrega y lo marca <c>entregado</c> solo si el destino respondió 202 (al menos una vez). Los registros llevan tipo, identificador y
/// resultado, nunca la carga.
/// </summary>
internal sealed partial class ProcesadorDeOutbox(IOutboxStore store, IEntregaInterna delivery, TimeProvider clock, ILogger<ProcesadorDeOutbox> logger)
{
    public async Task<bool> ProcessOneAsync(CancellationToken cancellationToken)
    {
        var evento = await store.ReserveAsync(clock.GetUtcNow().UtcDateTime, cancellationToken);
        if (evento is null)
        {
            return false;
        }

        ResultadoDeEntrega result;
        try
        {
            result = await delivery.DeliverAsync(evento, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            result = ResultadoDeEntrega.Reintentable;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var owned = result == ResultadoDeEntrega.Aceptado
            ? await store.MarkDeliveredAsync(evento, now, cancellationToken)
            : await store.FailAsync(evento, permanent: result == ResultadoDeEntrega.Rechazado, now, cancellationToken);
        var attempt = evento.Intentos + 1;
        var lastFailure = result == ResultadoDeEntrega.Rechazado || attempt >= Reintentos.PoliticaDeReintentos.MaxIntentos;
        var outcome = ProcesosMetrics.Outcome(owned, result == ResultadoDeEntrega.Aceptado, lastFailure, ProcesosMetrics.Entregado, ProcesosMetrics.Fallido);
        ProcesosMetrics.RecordAttempt(logger, ProcesosMetrics.Outbox, evento.Tipo, evento.Id, attempt, outcome);
        if (!owned)
        {
            LeaseLost(logger, evento.Tipo, evento.Id);
        }
        else if (result != ResultadoDeEntrega.Aceptado && lastFailure)
        {
            DeliveryFailed(logger, evento.Tipo, evento.Id);
        }

        return true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox lease lost: {EventType} {EventId}")]
    private static partial void LeaseLost(ILogger logger, string eventType, string eventId);

    [LoggerMessage(Level = LogLevel.Error, Message = "delivery_failed: outbox {EventType} {EventId}")]
    private static partial void DeliveryFailed(ILogger logger, string eventType, string eventId);
}

internal sealed class OutboxHostedService(
    IServiceScopeFactory scopes,
    IOptions<OutboxOptions> options,
    TimeProvider clock,
    ILogger<OutboxHostedService> logger) : ProcesoPeriodico(scopes, clock, logger, "outbox")
{
    protected override int IntervaloSegundos => options.Value.IntervaloSegundos;

    protected override Task RunStepAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<ProcesadorDeOutbox>().ProcessOneAsync(cancellationToken);
}
