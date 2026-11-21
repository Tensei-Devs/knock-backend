using Knock.Market.Api.Common.TareasProgramadas;
using Knock.Market.Api.Modules.Requests.Data;
using Knock.Market.Api.Modules.Requests.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace Knock.Market.Api.Modules.Requests.Services;

/// <summary>Tarea <c>VencerSolicitud</c> (spec 029, R-09): vence la solicitud de su clave de negocio. Sin efectos si ya no cumple la condición (cita, cancelada o ya vencida).</summary>
internal sealed class VencerSolicitud(CierreDeSolicitudService service) : IManejadorDeTarea
{
    public string Tipo => "VencerSolicitud";

    public async Task HandleAsync(TareaReservada task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (!ObjectId.TryParse(task.ClaveNegocio, out var id))
        {
            throw new FormatException("Invalid expiry task key.");
        }

        await service.ExpireAsync(id, cancellationToken);
    }
}

/// <summary>
/// Tarea recurrente <c>BarrerVencidas</c> (spec 029, R-10): aplica la lógica de R-09 a hasta 100 solicitudes con <c>venceEn</c> cumplido y programa el siguiente barrido
/// con una clave nueva (la tarea en curso se marca como hecha al terminar).
/// </summary>
internal sealed class BarrerVencidas(
    CierreDeSolicitudService service,
    CierreDeSolicitudStore store,
    TareasProgramadasStore tasks,
    IOptions<SolicitudesOptions> options,
    TimeProvider clock) : IManejadorDeTarea
{
    public const string TypeName = "BarrerVencidas";

    public string Tipo => TypeName;

    public async Task HandleAsync(TareaReservada task, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var id in await store.FindExpiredAsync(now, SolicitudesOptions.SweepBatch, cancellationToken))
        {
            await service.ExpireAsync(id, cancellationToken);
        }

        var next = now.AddMinutes(options.Value.MinutosBarrido);
        await tasks.ScheduleByKeyAsync(null, TypeName, "recurrente:" + new DateTimeOffset(next, TimeSpan.Zero).ToUnixTimeSeconds(), next, now, cancellationToken);
    }
}
