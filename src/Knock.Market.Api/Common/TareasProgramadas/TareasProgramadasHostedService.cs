using Knock.Hosting.Procesos;
using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Common.TareasProgramadas;

internal sealed class TareasProgramadasOptions
{
    public int IntervaloSegundos { get; set; } = 15;
}

internal sealed class TareasProgramadasHostedService(
    IServiceScopeFactory scopes,
    IOptions<TareasProgramadasOptions> options,
    TimeProvider clock,
    ILogger<TareasProgramadasHostedService> logger) : ProcesoPeriodico(scopes, clock, logger, "scheduled-tasks")
{
    protected override int IntervaloSegundos => options.Value.IntervaloSegundos;

    protected override Task RunStepAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<EjecutorDeTareasProgramadas>().ExecuteOneAsync(cancellationToken);
}
