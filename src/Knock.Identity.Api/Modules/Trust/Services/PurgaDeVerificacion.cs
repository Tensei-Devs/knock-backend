using Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;
using Knock.Identity.Api.Modules.Moderation.Services;
using Knock.Identity.Api.Modules.Panel;
using Knock.Identity.Api.Modules.Trust.Data;
using Knock.Identity.Api.Modules.Trust.Domain;
using MongoDB.Bson;

namespace Knock.Identity.Api.Modules.Trust.Services;

/// <summary>
/// La purga de fotos de verificación (spec 057, R-01): destruye la llave y deja el estado final en una transacción con la auditoría y el evento, y después borra los blobs por el puerto
/// <see cref="IVerificationBlobPurger"/>. Es idempotente (R-06): repetirla no cambia nada ni duplica evento o auditoría, y si los blobs fallaron la repetición los vuelve a intentar con la llave
/// ya destruida (las fotos ya son ilegibles). SEGURIDAD: nada de lo que escribe lleva CURP, HMAC, rutas, enlaces ni llave; solo identificadores y razón.
/// </summary>
internal sealed class PurgaDeVerificacion(IVerificationPurgeStore store, IVerificationBlobPurger blobs, IPanelApi panel, TimeProvider clock)
{
    /// <summary>Purga una verificación por <paramref name="reason"/> (<see cref="PurgeReasons"/>). Si el borrado de blobs falla lanza la excepción: la tarea la reintenta.</summary>
    public async Task<PurgeOutcome> PurgeAsync(ObjectId id, string reason, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var resourceId = id.ToString();
        var now = clock.GetUtcNow().UtcDateTime;
        now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMillisecond));
        var outcome = await store.PurgeAsync(
            id,
            reason,
            now,
            session => panel.RecordActionInTransactionAsync(session, PurgeEvents.AuditPurged, PanelApiAuditValues.ResultAllowed, actor, resourceId, cancellationToken),
            cancellationToken);
        if (outcome is PurgeOutcome.Purged or PurgeOutcome.AlreadyPurged)
        {
            await blobs.PurgeBlobsAsync(resourceId, cancellationToken);
        }

        return outcome;
    }

    /// <summary>Baja de cuenta (spec 039, R-25): la misma purga para cada verificación de la persona que aún conserve fotos o llave.</summary>
    public async Task PurgeAccountAsync(string sub, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);
        foreach (var id in await store.FindUnpurgedAsync(sub, cancellationToken))
        {
            await PurgeAsync(id, PurgeReasons.AccountDeletion, PurgeReasons.SystemActor, cancellationToken);
        }
    }
}

/// <summary>Tarea <c>AvisarAlVerificador</c> (24 h): alerta una sola vez si sigue en revisión; si ya se decidió termina sin efecto (spec 054, R-09).</summary>
internal sealed class AvisarAlVerificadorHandler(IVerificationPurgeStore store, TimeProvider clock) : IManejadorDeTarea
{
    public string Tipo => PurgeEvents.AlertTask;

    public async Task HandleAsync(TareaReservada task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (ObjectId.TryParse(task.ClaveNegocio, out var id))
        {
            await store.MarkAlertedAsync(id, clock.GetUtcNow().UtcDateTime, cancellationToken);
        }
    }
}

/// <summary>Tarea <c>BorrarFotosDeVerificacion</c> (7 días): purga por vencimiento y devuelve la verificación a <c>sin_verificar</c>; si ya se decidió, solo repite el borrado de blobs pendiente.</summary>
internal sealed class BorrarFotosDeVerificacionHandler(PurgaDeVerificacion purge) : IManejadorDeTarea
{
    public string Tipo => PurgeEvents.DeleteTask;

    public async Task HandleAsync(TareaReservada task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (ObjectId.TryParse(task.ClaveNegocio, out var id))
        {
            await purge.PurgeAsync(id, PurgeReasons.Expired, PurgeReasons.SystemActor, cancellationToken);
        }
    }
}
