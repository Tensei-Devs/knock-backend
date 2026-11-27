using Microsoft.Extensions.Options;

namespace Knock.Hosting.Procesos;

/// <summary>Un evento o una tarea en alerta. <see cref="Id"/> es el <c>_id</c> de la base y no se publica tal cual: <see cref="Resultado"/> es <c>fallido</c>, <c>fallida</c> o <c>atrasado</c>.</summary>
public sealed record AlertaDeProceso(string Proceso, string Tipo, string Id, int Intentos, string Resultado);

/// <summary>Lee los eventos y las tareas en alerta de la base de un servicio (spec 054, R-13).</summary>
public interface IConsultaDeAlertas
{
    /// <summary>Los fallidos y los que debían procesarse antes de <paramref name="atrasadasAntesDe"/> y siguen sin terminar.</summary>
    Task<IReadOnlyList<AlertaDeProceso>> FindAsync(DateTime atrasadasAntesDe, CancellationToken cancellationToken);
}

/// <summary>
/// Evalúa la alerta de la spec 054 (R-13, CA-20): cada evento o tarea fallido o con más de 10 minutos de retraso suma en la métrica
/// <c>knock.procesos.alertas</c> en cada evaluación, y se registra una vez mientras siga en alerta.
/// </summary>
internal sealed partial class VigilanteDeProcesos(TimeProvider clock, ILogger<VigilanteDeProcesos> logger)
{
    public static readonly TimeSpan RetrasoMaximo = TimeSpan.FromMinutes(10);

    private HashSet<AlertaDeProceso> _reported = [];

    public async Task EvaluateAsync(IEnumerable<IConsultaDeAlertas> queries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queries);
        var lateBefore = clock.GetUtcNow().UtcDateTime - RetrasoMaximo;
        var current = new HashSet<AlertaDeProceso>();
        foreach (var query in queries)
        {
            current.UnionWith(await query.FindAsync(lateBefore, cancellationToken));
        }

        foreach (var alert in current)
        {
            ProcesosMetrics.RecordAlert(alert);
            if (!_reported.Contains(alert))
            {
                var published = ProcesosMetrics.Identificador(alert.Id);
                ProcessAlert(logger, alert.Proceso, alert.Tipo, published, alert.Intentos, alert.Resultado);
            }
        }

        _reported = current;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "process_alert: {Process} {Type} {Id} attempt {Attempt} result {Result}")]
    private static partial void ProcessAlert(ILogger logger, string process, string type, string id, int attempt, string result);
}

/// <summary>Cada cuánto se evalúa la alerta: el mismo <c>TareasProgramadas:IntervaloSegundos</c> del ejecutor (15 por omisión).</summary>
internal sealed class VigilanciaDeProcesosOptions
{
    public int IntervaloSegundos { get; set; } = 15;
}

/// <summary>Proceso aparte de la bandeja y del ejecutor, para que la alerta salga aunque uno de ellos esté detenido en un paso.</summary>
internal sealed class VigilanciaDeProcesosHostedService(
    IServiceScopeFactory scopes,
    IOptions<VigilanciaDeProcesosOptions> options,
    TimeProvider clock,
    ILogger<VigilanciaDeProcesosHostedService> logger) : ProcesoPeriodico(scopes, clock, logger, "process-alerts")
{
    protected override int IntervaloSegundos => options.Value.IntervaloSegundos;

    protected override Task RunStepAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<VigilanteDeProcesos>().EvaluateAsync(services.GetServices<IConsultaDeAlertas>(), cancellationToken);
}

public static class VigilanciaDeProcesosExtensions
{
    /// <summary>Registra la evaluación de la alerta; cada servicio agrega su <see cref="IConsultaDeAlertas"/>. No registra comprobaciones de salud (R-14).</summary>
    public static IServiceCollection AddKnockVigilanciaDeProcesos(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<VigilanciaDeProcesosOptions>().BindConfiguration("TareasProgramadas");
        services.AddSingleton<VigilanteDeProcesos>();
        services.AddHostedService<VigilanciaDeProcesosHostedService>();
        return services;
    }
}
