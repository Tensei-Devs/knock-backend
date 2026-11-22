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
            await store.AbortAsync(task, clock.GetUtcNow().UtcDateTime, cancellationToken);
            UnknownType(logger, task.Tipo, task.Id.ToString());
            return true;
        }

        try
        {
            await handler.HandleAsync(task, cancellationToken);
            if (!await store.CompleteAsync(task, clock.GetUtcNow().UtcDateTime, cancellationToken))
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
        if (!await store.FailAsync(task, clock.GetUtcNow().UtcDateTime, cancellationToken))
        {
            LeaseLost(logger, task.Id.ToString());
        }
        else if (task.Intentos + 1 >= PoliticaDeReintentos.MaxIntentos)
        {
            HandlerFailed(logger, task.Tipo, task.Id.ToString());
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scheduled task lease lost: {TaskId}")]
    private static partial void LeaseLost(ILogger logger, string taskId);

    [LoggerMessage(Level = LogLevel.Error, Message = "handler_failed: scheduled task {TaskType} {TaskId}")]
    private static partial void HandlerFailed(ILogger logger, string taskType, string taskId);

    [LoggerMessage(Level = LogLevel.Error, Message = "unknown_task_type: scheduled task {TaskType} {TaskId}")]
    private static partial void UnknownType(ILogger logger, string taskType, string taskId);
}
