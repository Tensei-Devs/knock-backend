using Knock.Market.Api.Modules.Requests.Data;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;
using MongoDB.Bson;

namespace Knock.Market.Api.Modules.Requests.Services;

/// <summary>
/// Cierre de la visita (spec 050, VIS-09 a VIS-11): resuelve el papel de la persona por el <c>sub</c> del token (R-31), aplica las reglas de estado y de plazo con el reloj del
/// servidor y delega las escrituras atómicas en <see cref="VisitCloseStore"/>. No toca MongoDB y no registra textos ni fotos de las personas (R-32). Una visita ajena responde
/// como una inexistente.
/// </summary>
internal sealed class VisitCloseService(VisitCloseStore store, VisitStore visits, ITechnicianDirectory technicians, IVisitPhotos photos, TimeProvider clock)
{
    /// <summary>VIS-09: el técnico de la visita <c>en_curso</c> la termina con descripción y de 1 a 5 fotos suyas listas. No requiere confirmación del cliente.</summary>
    public async Task<VisitScheduleOutcome<FinishVisitResponse>> FinishAsync(string sub, string id, FinishVisitBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!await technicians.ExistsAsync(sub, cancellationToken))
        {
            return new(VisitScheduleStatus.NotATechnician);
        }

        var visit = await ResolveAsync(sub, id, VisitRole.Technician, cancellationToken);
        if (visit is null)
        {
            return new(VisitScheduleStatus.NotFound);
        }

        if (visit.Estado != VisitRules.InProgress)
        {
            return new(VisitScheduleStatus.NotInProgress);
        }

        var photoIds = body.Fotos!.Select(photo => ObjectId.Parse(photo).ToString()).ToList();
        if (!await photos.AreReadyForAsync(sub, photoIds, cancellationToken))
        {
            return new(VisitScheduleStatus.InvalidProposal);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var recommendation = string.IsNullOrWhiteSpace(body.Recomendacion) ? null : body.Recomendacion.Trim();
        return await store.FinishAsync(visit, body.Descripcion!.Trim(), recommendation, photoIds, now, cancellationToken)
            ? new(VisitScheduleStatus.Done, new FinishVisitResponse(visit.Id.ToString(), VisitCloseRules.Finished, new DateTimeOffset(now, TimeSpan.Zero)))
            : new(VisitScheduleStatus.NotInProgress);
    }

    /// <summary>VIS-10: el cliente dueño reporta que no se terminó, una sola vez y hasta 7 días después de terminada la visita.</summary>
    public async Task<VisitScheduleOutcome<NotFinishedResponse>> ReportNotFinishedAsync(string sub, string id, string text, CancellationToken cancellationToken)
    {
        var visit = await ResolveAsync(sub, id, VisitRole.Client, cancellationToken);
        if (visit is null)
        {
            return new(VisitScheduleStatus.NotFound);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (visit.Estado != VisitCloseRules.Finished || visit.NotFinishedReported || visit.FinishedAtUtc is not { } finishedAt || !VisitCloseRules.IsInReportWindow(now, finishedAt))
        {
            return new(VisitScheduleStatus.ReportWindowClosed);
        }

        return await store.ReportNotFinishedAsync(visit, text.Trim(), now, cancellationToken)
            ? new(VisitScheduleStatus.Done, new NotFinishedResponse(visit.Id.ToString(), VisitCloseRules.UnderReview))
            : new(VisitScheduleStatus.ReportWindowClosed);
    }

    /// <summary>VIS-11: el cliente dueño autoriza o no publicar las fotos del trabajo (por omisión, no). Repetir el valor no cambia nada.</summary>
    public async Task<VisitScheduleOutcome<PhotoPermissionResponse>> SetPhotoPermissionAsync(string sub, string id, bool? allowed, CancellationToken cancellationToken)
    {
        var visit = await ResolveAsync(sub, id, VisitRole.Client, cancellationToken);
        if (visit is null)
        {
            return new(VisitScheduleStatus.NotFound);
        }

        var value = allowed ?? false;
        await store.SetPhotoPermissionAsync(visit, value, clock.GetUtcNow().UtcDateTime, cancellationToken);
        return new(VisitScheduleStatus.Done, new PhotoPermissionResponse(visit.Id.ToString(), value));
    }

    private async Task<VisitRecord?> ResolveAsync(string sub, string id, VisitRole required, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var visitId))
        {
            throw new Common.Errors.DomainException(Common.Errors.DomainErrorKind.BadRequest, "The visit id is not valid.");
        }

        var visit = await visits.FindAsync(visitId, cancellationToken);
        return visit is not null && VisitAccess.RoleOf(sub, visit.ClientId, visit.TechnicianUserId) == required ? visit : null;
    }
}
