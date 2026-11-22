using Knock.Identity.Api.Modules.Moderation.Domain;
using Knock.Identity.Api.Modules.Panel;
using Knock.Identity.Api.Modules.Safety;

namespace Knock.Identity.Api.Modules.Moderation.Services;

/// <summary>La respuesta de la gravedad (spec 053, R-20): traduce la respuesta derivada a la acción de Safety, que la aplica con la suspensión de la spec 059 y los avisos de la 054.</summary>
internal sealed class ReportDecisionEffects(ISafetyApi safety) : IReportDecisionEffects
{
    public Task<IReadOnlyList<string>> ApplyAsync(StoreTransaction transaction, ReportDecisionApplied decision, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var action = decision.Response switch
        {
            ReportResponses.PreventiveSuspension => ReportAction.Suspend,
            ReportResponses.Hide => ReportAction.Hide,
            _ => ReportAction.NotifyOnly,
        };
        return safety.ApplyReportDecisionAsync(transaction, action, decision.ReportId, decision.ReporterId, decision.ReportedId, decision.DeciderSub, cancellationToken);
    }

    public Task AfterCommitAsync(IReadOnlyList<string> sessionIds, CancellationToken cancellationToken) => safety.CancelSessionsAsync(sessionIds, cancellationToken);
}
