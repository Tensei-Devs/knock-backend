using Knock.Identity.Api.Hosting.Procesos.Outbox;
using Knock.Identity.Api.Modules.Panel;
using Knock.Identity.Api.Modules.Safety.Data;
using Knock.Identity.Api.Modules.Safety.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace Knock.Identity.Api.Modules.Safety.Services;

/// <summary>
/// La respuesta real de la gravedad de un reporte (spec 053, R-20). Suspender usa <see cref="AccountSuspension"/> (spec 059; a un cliente no, R-03), ocultar pide <c>TecnicoOcultado</c> a Market y
/// toda decisión avisa a las dos partes con <c>ReporteDecidido</c> sin datos de la otra. Los cambios se escriben en la transacción de la decisión; los eventos llevan solo identificadores (spec 054, R-02).
/// </summary>
internal sealed class SafetyApi(AccountSuspension suspension, ISuspensionStore store, IWarningCounter warnings, IOptions<SuspensionOptions> options, TimeProvider clock) : ISafetyApi
{
    public const string NoticeEvent = "ReporteDecidido";
    public const string HideEvent = "TecnicoOcultado";

    public async Task<IReadOnlyList<string>> ApplyReportDecisionAsync(StoreTransaction transaction, ReportAction action, string reportId, string reporterId, string reportedId, string deciderSub, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> sessionIds = [];
        var events = new List<EventoDeOutbox>();
        if (action == ReportAction.Suspend)
        {
            sessionIds = (await suspension.ApplySuspensionAsync(transaction, reportedId, reportId, deciderSub, "preventiva", cancellationToken)).SessionIds;
        }
        else if (action == ReportAction.Warn)
        {
            sessionIds = await ApplyGradualAsync(transaction, reportId, reportedId, deciderSub, cancellationToken);
        }
        else if (action == ReportAction.Hide)
        {
            var hideId = OutboxReglas.BuildId(HideEvent, reportId, 1);
            events.Add(new EventoDeOutbox(hideId, HideEvent, "market", new BsonDocument { ["eventoId"] = hideId, ["usuarioId"] = reportedId, ["reporteId"] = reportId }));
        }

        events.Add(Notice(reportId, reporterId, "reportante", action));
        events.Add(Notice(reportId, reportedId, "reportado", action));
        await store.AddEventsAsync(transaction, events, clock.GetUtcNow().UtcDateTime, cancellationToken);
        return sessionIds;
    }

    // Regla gradual (spec 059, R-16): apagada sin umbral configurado. Cuenta las advertencias decididas y vigentes, incluida la de esta misma transacción, y al llegar al umbral suspende como en R-04.
    private async Task<IReadOnlyList<string>> ApplyGradualAsync(StoreTransaction transaction, string reportId, string reportedId, string deciderSub, CancellationToken cancellationToken)
    {
        var threshold = options.Value.UmbralAdvertencias;
        if (threshold is null || !AccountSuspensionRules.ShouldApplyGradual(await warnings.CountDecidedWarningsAsync(transaction, reportedId, clock.GetUtcNow().UtcDateTime.AddDays(-options.Value.VigenciaAdvertenciasDias), cancellationToken), threshold))
        {
            return [];
        }

        return (await suspension.ApplySuspensionAsync(transaction, reportedId, reportId, deciderSub, SuspensionTypes.Gradual, cancellationToken)).SessionIds;
    }

    public async Task CancelSessionsAsync(IReadOnlyList<string> sessionIds, CancellationToken cancellationToken) => await suspension.CancelSessionsAsync(sessionIds, cancellationToken);

    // Cada parte recibe solo su rol y la acción: nunca el identificador, el motivo ni el texto de la otra (R-20).
    private static EventoDeOutbox Notice(string reportId, string recipient, string role, ReportAction action)
    {
        var id = OutboxReglas.BuildId(NoticeEvent, $"{reportId}:{role}", 1);
        return new EventoDeOutbox(id, NoticeEvent, "messaging", new BsonDocument { ["eventoId"] = id, ["usuarioId"] = recipient, ["reporteId"] = reportId, ["rol"] = role, ["accion"] = (action == ReportAction.Warn ? ReportAction.NotifyOnly : action).ToString() });
    }
}
