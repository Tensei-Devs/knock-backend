namespace Knock.Market.Api.Common.TareasProgramadas;

internal interface IManejadorDeTarea
{
    string Tipo { get; }
    Task HandleAsync(TareaReservada task, CancellationToken cancellationToken);
}

internal sealed partial class EjecutorDeTareasProgramadas(
    ITareasProgramadasStore store,
    IEnumerable<IManejadorDeTarea> handlers,
    TimeProvider clock,
    ILogger<EjecutorDeTareasProgramadas> logger)
{
    private readonly Dictionary<string, IManejadorDeTarea> _handlers = handlers.ToDictionary(x => x.Tipo, StringComparer.Ordinal);

    public async Task<bool> ExecuteOneAsync(CancellationToken cancellationToken)
    {
        var types = _handlers.Keys.ToArray();
        var reserved = types.Length == 0 ? null : await store.ReserveAsync(clock.GetUtcNow().UtcDateTime, cancellationToken, types);
        if (reserved is null)
        {
            return false;
        }

        await RunAsync(reserved, cancellationToken);
        return true;
    }

    private async Task RunAsync(TareaReservada task, CancellationToken cancellationToken)
    {
        try
        {
            await _handlers[task.Tipo].HandleAsync(task, cancellationToken);
            var completed = await store.CompleteAsync(task, clock.GetUtcNow().UtcDateTime, cancellationToken);
            if (!completed)
            {
                LeaseLost(logger, task.Id.ToString());
            }
        }
        catch (Exception error) when (!(error is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            await RecordFailureAsync(task, cancellationToken);
        }
    }

    private async Task RecordFailureAsync(TareaReservada task, CancellationToken cancellationToken)
    {
        if (!await store.FailAsync(task, clock.GetUtcNow().UtcDateTime, cancellationToken))
        {
            LeaseLost(logger, task.Id.ToString());
        }
        else if (task.Intentos + 1 >= 5)
        {
            TaskFailed(logger, task.Tipo, task.Id.ToString());
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scheduled task lease lost: {TaskId}")]
    private static partial void LeaseLost(ILogger logger, string taskId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Scheduled task failed: {TaskType} {TaskId}")]
    private static partial void TaskFailed(ILogger logger, string taskType, string taskId);
}
