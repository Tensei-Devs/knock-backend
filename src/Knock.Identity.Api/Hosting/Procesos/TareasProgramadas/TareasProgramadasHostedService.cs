// jscpd:ignore-start -- EXC-015: copia de src/Knock.Market.Api/Common/TareasProgramadas (spec 028 R-09); se unifica en la biblioteca técnica (KNOCK-251).
using Microsoft.Extensions.Options;

namespace Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;

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
// jscpd:ignore-end
