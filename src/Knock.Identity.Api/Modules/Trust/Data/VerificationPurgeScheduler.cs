using Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;
using Knock.Identity.Api.Modules.Trust.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Identity.Api.Modules.Trust.Data;

/// <summary>
/// Programa al enviar la verificación (spec 025, R-13) las dos tareas de la spec 057: <c>AvisarAlVerificador</c> a las 24 h y <c>BorrarFotosDeVerificacion</c> a los 7 días, con
/// <c>claveNegocio = verificacionId</c>. Programarlas dos veces no duplica (índice único de la spec 054). Se llama dentro de la transacción del envío.
/// </summary>
internal sealed class VerificationPurgeScheduler(ITareasProgramadasStore tasks, TimeProvider clock)
{
    public async Task ScheduleAsync(IClientSessionHandle? session, ObjectId verificationId, DateTime sentAt, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var type in new[] { PurgeEvents.AlertTask, PurgeEvents.DeleteTask })
        {
            await tasks.ScheduleAsync(session, type, verificationId.ToString(), CatalogoDeTareas.ProgramadaPara(type, sentAt), now, cancellationToken);
        }
    }
}
