namespace Knock.Market.Api.Hosting;

/// <summary>Un registro de la auditoría del panel (spec 045, R-20): quién, cuándo y qué cambió. <paramref name="Changes"/> lleva nombres de campo y valores de catálogo, nunca datos de personas.</summary>
internal sealed record PanelAuditEntry(string Subject, string Action, string Resource, DateTimeOffset At, IReadOnlyDictionary<string, string> Changes);

/// <summary>
/// Frontera hacia la auditoría del panel (spec 033), que vive en Identidad. La implementación real llamará a Identidad con la autenticación entre servicios (spec 056). Mientras no exista,
/// <see cref="UnavailablePanelAuditTrail"/> falla cerrado (devuelve falso): sin auditoría no se altera el catálogo.
/// </summary>
internal interface IPanelAuditTrail
{
    /// <summary>Registra la entrada. Devuelve <see langword="false"/> si no se pudo dejar constancia: la operación no debe realizarse.</summary>
    Task<bool> RecordAsync(PanelAuditEntry entry, CancellationToken cancellationToken);
}

/// <summary>Implementación por omisión: falla cerrado. Las altas y cambios del catálogo no funcionan hasta registrar la implementación real.</summary>
internal sealed class UnavailablePanelAuditTrail : IPanelAuditTrail
{
    public Task<bool> RecordAsync(PanelAuditEntry entry, CancellationToken cancellationToken) => Task.FromResult(false);
}
