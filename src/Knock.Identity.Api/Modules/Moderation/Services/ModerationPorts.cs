using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Moderation.Data;
using Knock.Identity.Api.Modules.Moderation.Domain;
using Knock.Identity.Api.Modules.Moderation.Dtos;
using Knock.Identity.Api.Modules.Panel;
using MongoDB.Bson;

namespace Knock.Identity.Api.Modules.Moderation.Services;

// Contratos con las dependencias de la spec 053 que otras specs implementan (025 T-04, 039, 057, 059, 054). Cada uno trae un valor por omisión que FALLA CERRADO o no hace nada
// inofensivo, para que el módulo compile, se pruebe y se despliegue sin ellas; la spec dueña reemplaza el registro (TryAdd) sin tocar este módulo.

/// <summary>
/// Entrega los enlaces de lectura de 5 minutos de las fotos de una verificación (spec 025, T-04: <c>FotoVerificacionStore</c>, SAS de solo lectura). SEGURIDAD: los enlaces no se guardan,
/// no se cachean y no se registran (R-17, R-21).
/// </summary>
internal interface IVerificationPhotoLinks
{
    Task<IReadOnlyList<VerificationPhotoLink>> IssueAsync(string verificationId, IReadOnlyList<string> photoTypes, TimeSpan lifetime, CancellationToken cancellationToken);
}

/// <summary>Sin el almacenamiento privado de la spec 025 no hay fotos que enlazar: responde 503 y no inventa ningún enlace.</summary>
internal sealed class UnavailableVerificationPhotoLinks : IVerificationPhotoLinks
{
    public Task<IReadOnlyList<VerificationPhotoLink>> IssueAsync(string verificationId, IReadOnlyList<string> photoTypes, TimeSpan lifetime, CancellationToken cancellationToken) =>
        throw new DomainException(DomainErrorKind.ServiceUnavailable, "The private photo storage is not available.", ModerationErrorCodes.PhotosUnavailableCode);
}

/// <summary>
/// Borra los blobs de una verificación ya decidida (spec 057, <c>PurgaDeVerificacion</c>). Se invoca después de confirmar la decisión: la llave ya se destruyó en esa transacción, así que las
/// fotos son ilegibles aunque un blob sobreviva; un fallo aquí no revierte la decisión y la tarea de la spec 054 lo reintenta.
/// </summary>
internal interface IVerificationBlobPurger
{
    Task PurgeBlobsAsync(string verificationId, CancellationToken cancellationToken);
}

/// <summary>Mientras no exista el almacén de fotos (spec 025, T-04) no hay blobs que borrar.</summary>
internal sealed class NoVerificationBlobs : IVerificationBlobPurger
{
    public Task PurgeBlobsAsync(string verificationId, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Resultado de decidir una recuperación de acceso (spec 039).</summary>
internal enum RecoveryDecisionOutcome
{
    NotFound,
    AlreadyDecided,
    Decided,
}

/// <summary>
/// Casos de recuperación de acceso que comparten la cola de verificaciones (spec 053, D-07; spec 039). Aprobar uno cambia el número de la cuenta y cierra sus sesiones en la misma transacción
/// que la auditoría (R-22); eso lo implementa la spec 039.
/// </summary>
internal interface IAccessRecoveryReviews
{
    Task<IReadOnlyList<QueuedVerification>> ListAsync(string state, DateTime from, DateTime to, ReviewCursor? after, int take, CancellationToken cancellationToken);

    Task<RecoveryDecisionOutcome> DecideAsync(ObjectId id, bool approved, string deciderSub, Func<StoreTransaction, Task> writeAudit, CancellationToken cancellationToken);
}

/// <summary>Sin la spec 039 la cola de recuperaciones está vacía y ningún identificador es una recuperación.</summary>
internal sealed class NoAccessRecoveryReviews : IAccessRecoveryReviews
{
    public Task<IReadOnlyList<QueuedVerification>> ListAsync(string state, DateTime from, DateTime to, ReviewCursor? after, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<QueuedVerification>>([]);

    public Task<RecoveryDecisionOutcome> DecideAsync(ObjectId id, bool approved, string deciderSub, Func<StoreTransaction, Task> writeAudit, CancellationToken cancellationToken) =>
        Task.FromResult(RecoveryDecisionOutcome.NotFound);
}

/// <summary>Lo que se decidió sobre un reporte, con los identificadores que necesitan los efectos y nada más (sin textos ni datos de las personas).</summary>
internal sealed record ReportDecisionApplied(string ReportId, string Severity, string Response, string ReporterId, string ReportedId, string DeciderSub);

/// <summary>
/// La respuesta de la gravedad (spec 053, R-20): suspensión preventiva (spec 059), ocultar al técnico, advertencia o mediación, y los avisos a las dos partes sin datos de la otra (specs 054 y 037).
/// <see cref="ApplyAsync"/> corre DENTRO de la transacción de la decisión: si falla no queda nada. Devuelve los <c>sid</c> que hay que cancelar en Redis y
/// <see cref="AfterCommitAsync"/> lo hace después de confirmar (spec 059, R-08).
/// </summary>
internal interface IReportDecisionEffects
{
    Task<IReadOnlyList<string>> ApplyAsync(StoreTransaction transaction, ReportDecisionApplied decision, CancellationToken cancellationToken);

    Task AfterCommitAsync(IReadOnlyList<string> sessionIds, CancellationToken cancellationToken);
}

/// <summary>Sin efectos: la decisión se guarda y se audita sin más. Lo usan las pruebas que no ejercen la respuesta de la gravedad.</summary>
internal sealed class NoReportDecisionEffects : IReportDecisionEffects
{
    public Task<IReadOnlyList<string>> ApplyAsync(StoreTransaction transaction, ReportDecisionApplied decision, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);

    public Task AfterCommitAsync(IReadOnlyList<string> sessionIds, CancellationToken cancellationToken) => Task.CompletedTask;
}
