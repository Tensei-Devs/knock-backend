using Knock.Hosting.Procesos;
using Knock.Market.Api.Modules.Assistant.Data;

namespace Knock.Market.Api.Modules.Assistant.Services;

/// <summary>
/// Spec 052, R-21: una bitácora guardada con la solicitud abierta no tiene fecha de borrado. Cuando la solicitud se cierra, por cualquier vía, aquí se le pone <c>venceEn</c> (cierre más 30 días) y el índice TTL la
/// borra. Recorre todas las que no tienen fecha, por lotes, para que las de solicitudes abiertas no tapen a las demás. Repetirlo o correrlo en dos réplicas no cambia el resultado.
/// </summary>
internal sealed class FechadoDeBitacoras(ISolicitudesAsistente requests, IBitacoraStore store)
{
    public const int Lote = 100;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        string? after = null;
        IReadOnlyList<BitacoraSinFecha> page;
        do
        {
            page = await store.ListWithoutExpiryAsync(after, Lote, cancellationToken);
            if (page.Count == 0)
            {
                return;
            }

            var closed = await requests.GetClosedAtAsync([.. page.Select(x => x.SolicitudId).Distinct(StringComparer.Ordinal)], cancellationToken);
            foreach (var (solicitudId, closedAtUtc) in closed)
            {
                await store.SetExpiryAsync(solicitudId, closedAtUtc.Add(BitacoraService.Retention), cancellationToken);
            }

            after = page[^1].Id;
        }
        while (page.Count == Lote);
    }
}

/// <summary>Revisión cada 15 minutos: el plazo es de 30 días, así que no hace falta más seguido. No es una tarea de <c>tareas_programadas</c> (su catálogo es cerrado, spec 054, R-08).</summary>
internal sealed class FechadoDeBitacorasHostedService(IServiceScopeFactory scopes, TimeProvider clock, ILogger<FechadoDeBitacorasHostedService> logger)
    : ProcesoPeriodico(scopes, clock, logger, "assistant-log-expiry")
{
    private const int _intervalSeconds = 900;

    protected override int IntervaloSegundos => _intervalSeconds;

    protected override Task RunStepAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<FechadoDeBitacoras>().RunAsync(cancellationToken);
}
