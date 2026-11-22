using Knock.Market.Api.Modules.Tecnicos.Data;

namespace Knock.Market.Api.Modules.Tecnicos.Services;

/// <summary>
/// Consumidor de <c>TecnicoOcultado</c> (spec 053, R-20): cuando el panel decide un reporte de gravedad alta, Identidad avisa a Mercado y el técnico deja de aparecer en búsqueda, perfil público,
/// candidatos y cotizaciones. El evento lleva solo identificadores; entregarlo dos veces no cambia nada.
/// </summary>
internal sealed class TechnicianHideService(ITechnicianModerationStore store, TimeProvider clock)
{
    /// <summary>Oculta al técnico. Devuelve si cambió algo; un técnico que no existe no es error (el evento no se reintenta sin fin).</summary>
    public Task<bool> HideAsync(string usuarioId, string reporteId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usuarioId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reporteId);
        return store.HideAsync(usuarioId, reporteId, clock.GetUtcNow().UtcDateTime, cancellationToken);
    }
}
