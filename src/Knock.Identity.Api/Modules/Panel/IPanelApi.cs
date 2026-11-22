namespace Knock.Identity.Api.Modules.Panel;

/// <summary>Manija opaca de la transacción de MongoDB de un almacén: los servicios la pasan sin conocer el controlador de MongoDB (spec 003, CA-003-02); solo los almacenes la abren.</summary>
public readonly record struct StoreTransaction(object Handle);

/// <summary>Permisos del panel que otros módulos exigen en sus rutas (spec 033, R-02); la lista completa vive en el dominio del panel.</summary>
public static class PanelApiPermissions
{
    public const string VerificationReview = "verification:review";
    public const string ReportsResolve = "reports:resolve";
}

/// <summary>Valores de la auditoría del panel que otros módulos pueden escribir (spec 033, §6).</summary>
public static class PanelApiAuditValues
{
    public const string ResultAllowed = "permitido";
}

/// <summary>
/// Lo que el módulo del panel ofrece a los demás módulos de Identidad sin que lean su dominio ni sus almacenes (spec 033): escribir la auditoría de una acción
/// del panel, sola o dentro de la transacción de quien llama (spec 053, R-22). Falla cerrada: si no se puede escribir lanza 503 <c>audit_unavailable</c>.
/// </summary>
public interface IPanelApi
{
    /// <summary>Escribe un evento de tipo acción en la auditoría del panel.</summary>
    Task RecordActionAsync(string eventName, string result, string? subject, string? sessionId, string? permission, string? resourceId, CancellationToken cancellationToken);

    /// <summary>Escribe un evento de tipo acción dentro de la transacción de MongoDB de quien llama: el registro y el cambio se confirman juntos o ninguno.</summary>
    Task RecordActionInTransactionAsync(StoreTransaction transaction, string eventName, string result, string? subject, string? resourceId, CancellationToken cancellationToken);

    /// <summary>Revoca las sesiones abiertas de la cuenta del panel de ese <c>sub</c> dentro de la transacción de quien llama; no hace nada si no tiene cuenta del panel (spec 059, R-04 d).</summary>
    Task RevokeSessionsInTransactionAsync(StoreTransaction transaction, string subject, CancellationToken cancellationToken);
}

internal sealed class PanelApi(Services.AuditoriaPanel audit, Data.PanelSesionesStore? sessions = null) : IPanelApi
{
    public Task RecordActionAsync(string eventName, string result, string? subject, string? sessionId, string? permission, string? resourceId, CancellationToken cancellationToken) =>
        audit.WriteAsync(Data.PanelAuditValues.TipoAccion, eventName, result, subject, sessionId, permission, null, resourceId, cancellationToken);

    public Task RecordActionInTransactionAsync(StoreTransaction transaction, string eventName, string result, string? subject, string? resourceId, CancellationToken cancellationToken) =>
        audit.WriteInTransactionAsync(transaction, Data.PanelAuditValues.TipoAccion, eventName, result, subject, resourceId, cancellationToken);

    public async Task RevokeSessionsInTransactionAsync(StoreTransaction transaction, string subject, CancellationToken cancellationToken)
    {
        var store = sessions ?? throw new InvalidOperationException("The panel session store is not configured.");
        await store.RevokeAllInTransactionAsync(transaction, subject, cancellationToken);
    }
}
