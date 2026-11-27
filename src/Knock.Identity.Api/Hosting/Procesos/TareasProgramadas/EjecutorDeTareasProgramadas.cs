using Knock.Hosting.Procesos;
using Knock.Identity.Api.Hosting.Procesos.Reintentos;

namespace Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;

/// <summary>
/// Ejecutor de tareas de Identidad (spec 054, R-07, R-08 y R-12). Reserva una tarea vencida, la entrega a su manejador y la marca <c>hecha</c> o la reprograma. Un tipo fuera del catálogo no se ejecuta: queda
/// <c>fallida</c> y se registra. Un tipo del catálogo sin manejador registrado se deja pendiente hasta que la spec dueña lo agregue.
/// </summary>
internal sealed partial class EjecutorDeTareasProgramadas(
    ITareasProgramadasStore store,
    IEnumerable<IManejadorDeTarea> handlers,
    TimeProvider clock,
    ILogger<EjecutorDeTareasProgramadas> logger)
{
    private readonly Dictionary<string, IManejadorDeTarea> _handlers = handlers.ToDictionary(x => x.Tipo, StringComparer.Ordinal);

    public async Task<bool> ExecuteOneAsync(CancellationToken cancellationToken)
    {
        var excluded = CatalogoDeTareas.Tipos.Where(x => !_handlers.ContainsKey(x)).ToArray();
        var task = await store.ReserveAsync(clock.GetUtcNow().UtcDateTime, excluded, cancellationToken);
        if (task is null)
        {
            return false;
        }

        if (!_handlers.TryGetValue(task.Tipo, out var handler))
        {
            var aborted = await store.AbortAsync(task, clock.GetUtcNow().UtcDateTime, cancellationToken);
            Record(task, ProcesosMetrics.Outcome(aborted, succeeded: false, lastFailure: true, ProcesosMetrics.Hecha, ProcesosMetrics.Fallida));
            UnknownType(logger, task.Tipo, task.Id.ToString());
            return true;
        }

        try
        {
            await handler.HandleAsync(task, cancellationToken);
            var completed = await store.CompleteAsync(task, clock.GetUtcNow().UtcDateTime, cancellationToken);
            Record(task, ProcesosMetrics.Outcome(completed, succeeded: true, lastFailure: false, ProcesosMetrics.Hecha, ProcesosMetrics.Fallida));
            if (!completed)
            {
                LeaseLost(logger, task.Id.ToString());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            await RecordFailureAsync(task, cancellationToken);
        }

        return true;
    }

    private async Task RecordFailureAsync(TareaReservada task, CancellationToken cancellationToken)
    {
        var owned = await store.FailAsync(task, clock.GetUtcNow().UtcDateTime, cancellationToken);
        var lastFailure = task.Intentos + 1 >= PoliticaDeReintentos.MaxIntentos;
        Record(task, ProcesosMetrics.Outcome(owned, succeeded: false, lastFailure, ProcesosMetrics.Hecha, ProcesosMetrics.Fallida));
        if (!owned)
        {
            LeaseLost(logger, task.Id.ToString());
        }
        else if (lastFailure)
        {
            HandlerFailed(logger, task.Tipo, task.Id.ToString());
        }
    }

    private void Record(TareaReservada task, string outcome) =>
        ProcesosMetrics.RecordAttempt(logger, ProcesosMetrics.Tareas, task.Tipo, task.Id.ToString(), task.Intentos + 1, outcome);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scheduled task lease lost: {TaskId}")]
    private static partial void LeaseLost(ILogger logger, string taskId);

    [LoggerMessage(Level = LogLevel.Error, Message = "handler_failed: scheduled task {TaskType} {TaskId}")]
    private static partial void HandlerFailed(ILogger logger, string taskType, string taskId);

    [LoggerMessage(Level = LogLevel.Error, Message = "unknown_task_type: scheduled task {TaskType} {TaskId}")]
    private static partial void UnknownType(ILogger logger, string taskType, string taskId);
}
