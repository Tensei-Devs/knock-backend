using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Hosting.Procesos.Outbox;
using Knock.Identity.Api.Modules.Panel;
using Knock.Identity.Api.Modules.Safety.Data;
using Knock.Identity.Api.Modules.Safety.Domain;
using Knock.Identity.Api.Modules.Safety.Dtos;
using Knock.Identity.Api.Modules.Sesiones;
using MongoDB.Bson;

namespace Knock.Identity.Api.Modules.Safety.Services;

/// <summary>
/// Resolución de la apelación de una persona suspendida desde el panel (spec 060, R-05, R-06, R-10). Aceptar reactiva la cuenta con <see cref="AccountSuspension"/> (spec 059, R-15) en la misma transacción que
/// el cierre de la apelación, la auditoría y el evento <c>ApelacionResuelta</c>; rechazar deja la cuenta suspendida y cierra la apelación. Quien decidió el reporte solo la revisa si no hay otra persona
/// con el permiso (queda marcada «misma persona»). Ni el texto de la apelación ni el del reporte salen de aquí.
/// </summary>
internal sealed class SuspensionAppealResolutionService(
    ISuspensionStore transactions,
    SuspensionAppealStore store,
    AccountSuspension suspension,
    ISesionesApi accounts,
    IPanelApi panel,
    TimeProvider clock)
{
    public async Task<AppealResolutionResult> ResolveAsync(string actorSub, string reportId, string decision, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorSub);
        var id = ObjectId.Parse(reportId);
        var report = await store.FindAsync(id, cancellationToken);
        if (report is not { Appeal: { Origin: AppealOrigins.Suspension } appeal })
        {
            throw new DomainException(DomainErrorKind.NotFound, "The appeal does not exist.", SafetyErrorCodes.AppealNotFoundCode);
        }

        if (appeal.Decision is not null || report.State != ReportStates.Appealed)
        {
            throw AlreadyResolved();
        }

        var samePerson = string.Equals(actorSub, report.DeciderSub, StringComparison.OrdinalIgnoreCase);
        if (string.Equals(actorSub, report.ReportedId, StringComparison.OrdinalIgnoreCase)
            || (samePerson && await panel.AnotherAccountHasPermissionAsync(PanelApiPermissions.ReportsResolve, actorSub, cancellationToken)))
        {
            throw new DomainException(DomainErrorKind.Forbidden, "The person who decided cannot review this appeal.", CommonErrorCodes.SelfReview);
        }

        var accept = decision == AppealDecisions.Accept;
        var now = Millisecond(clock.GetUtcNow().UtcDateTime);
        await transactions.RunAsync(
            async transaction =>
            {
                if (!await store.ResolveAsync(transaction, id, decision, actorSub, samePerson, now, cancellationToken))
                {
                    throw AlreadyResolved();
                }

                // Reactiva solo si la cuenta sigue suspendida por este mismo reporte (puede haber vencido o haberse suspendido por otro).
                if (accept && await accounts.FindSuspensionBySubAsync(report.ReportedId, cancellationToken) is { } current && current.ReportId == reportId)
                {
                    await suspension.ApplyReactivationAsync(transaction, report.ReportedId, reportId, actorSub, cancellationToken);
                }

                await panel.RecordActionInTransactionAsync(transaction, accept ? AppealEvents.AuditAccepted : AppealEvents.AuditRejected, PanelApiAuditValues.ResultAllowed, actorSub, reportId, cancellationToken);
                var eventId = OutboxReglas.BuildId(AppealEvents.Resolved, reportId, 1);
                await transactions.AddEventsAsync(
                    transaction,
                    [new EventoDeOutbox(eventId, AppealEvents.Resolved, "identity", new BsonDocument { ["eventoId"] = eventId, ["reporteId"] = reportId, ["decision"] = decision })],
                    now,
                    cancellationToken);
                return true;
            },
            cancellationToken);
        return new AppealResolutionResult(reportId, ReportStates.Decided, decision);
    }

    private static DomainException AlreadyResolved() => new(DomainErrorKind.Conflict, "The appeal was already resolved.", SafetyErrorCodes.AppealAlreadyResolvedCode);

    private static DateTime Millisecond(DateTime value) => value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMillisecond));
}
