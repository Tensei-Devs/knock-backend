using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Knock.Hosting.Procesos;

/// <summary>Bucle de fondo con temporizador: cada tick corre un paso en su propio ámbito y un fallo no tumba el proceso (spec 054, R-14); lo comparten Identidad y Mercado.</summary>
public abstract partial class ProcesoPeriodico(IServiceScopeFactory scopes, TimeProvider clock, ILogger logger, string nombre) : BackgroundService
{
    protected abstract int IntervaloSegundos { get; }

    protected abstract Task RunStepAsync(IServiceProvider services, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(IntervaloSegundos), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await TickAsync(stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await RunStepAsync(scope.ServiceProvider, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (error is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            Unavailable(logger, nombre, error.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Background process {Process} unavailable: {ErrorType}")]
    private static partial void Unavailable(ILogger logger, string process, string errorType);
}
