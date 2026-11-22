using Knock.Identity.Api.Modules.Panel;

namespace Knock.Identity.Api.Modules.Safety;

/// <summary>Lo que se hace sobre la persona reportada al decidir un reporte (spec 053, R-20).</summary>
internal enum ReportAction
{
    /// <summary>Solo avisar a las dos partes (advertencia y mediación).</summary>
    NotifyOnly,

    /// <summary>Suspensión preventiva de un técnico (spec 059).</summary>
    Suspend,

    /// <summary>Ocultar al técnico de nuevas solicitudes.</summary>
    Hide,
}

/// <summary>
/// Lo que el módulo Safety ofrece a los demás módulos de Identidad sin que lean sus servicios ni almacenes (spec 059): aplicar la respuesta de la gravedad de un reporte dentro de la transacción
/// de quien decide (spec 053, R-20) y cancelar después de confirmar las sesiones revocadas (spec 059, R-08).
/// </summary>
internal interface ISafetyApi
{
    /// <summary>Aplica la acción y deja los avisos a las dos partes en la bandeja; devuelve los <c>sid</c> que <see cref="CancelSessionsAsync"/> debe cancelar tras confirmar.</summary>
    Task<IReadOnlyList<string>> ApplyReportDecisionAsync(StoreTransaction transaction, ReportAction action, string reportId, string reporterId, string reportedId, string deciderSub, CancellationToken cancellationToken);

    Task CancelSessionsAsync(IReadOnlyList<string> sessionIds, CancellationToken cancellationToken);
}
