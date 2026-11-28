using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Trust.Domain;

namespace Knock.Identity.Api.Modules.Trust.Storage;

/// <summary>
/// Puerto con la cuenta de almacenamiento PRIVADA de verificación (spec 025, ADR-007): contenedor de cuarentena (lo que sube la app con un SAS de escritura) y contenedor definitivo
/// (solo contenido cifrado). La implementación real (Azure Blob) es una dependencia de infraestructura; sin ella el valor por omisión falla cerrado.
/// </summary>
internal interface IVerificationBlobStore
{
    /// <summary>SAS de ESCRITURA de un solo blob de cuarentena, sin lectura ni listado, que vence en <paramref name="lifetime"/> (R-08).</summary>
    Task<Uri> IssueQuarantineUploadLinkAsync(string blobName, TimeSpan lifetime, CancellationToken cancellationToken);

    /// <summary>SAS de LECTURA de un solo blob definitivo (el contenido sigue cifrado) que vence en <paramref name="lifetime"/>.</summary>
    Task<Uri> IssueFinalReadLinkAsync(string blobName, TimeSpan lifetime, CancellationToken cancellationToken);

    /// <summary>Lee a lo sumo <paramref name="maxBytes"/> + 1 bytes del blob de cuarentena; <see langword="null"/> si no existe. Devolver más de <paramref name="maxBytes"/> significa «demasiado grande».</summary>
    Task<byte[]?> ReadQuarantineAsync(string blobName, int maxBytes, CancellationToken cancellationToken);

    /// <summary>Lee el blob definitivo; <see langword="null"/> si no existe.</summary>
    Task<byte[]?> ReadFinalAsync(string blobName, CancellationToken cancellationToken);

    /// <summary>Escribe el blob definitivo sin sobrescribir (<c>If-None-Match: *</c>); <see langword="false"/> si ya existía.</summary>
    Task<bool> WriteFinalAsync(string blobName, byte[] content, CancellationToken cancellationToken);

    /// <summary>Borrado idempotente: repetirlo no falla.</summary>
    Task DeleteQuarantineAsync(string blobName, CancellationToken cancellationToken);

    /// <summary>Borrado idempotente: repetirlo no falla.</summary>
    Task DeleteFinalAsync(string blobName, CancellationToken cancellationToken);
}

/// <summary>Sin cuenta de almacenamiento de verificación configurada no hay fotos: responde 503 y no inventa ningún enlace ni guarda nada.</summary>
internal sealed class UnavailableVerificationBlobStore : IVerificationBlobStore
{
    private static DomainException Unavailable() =>
        new(DomainErrorKind.ServiceUnavailable, "The private verification storage is not configured.", VerificationErrors.StorageUnavailableCode);

    public Task<Uri> IssueQuarantineUploadLinkAsync(string blobName, TimeSpan lifetime, CancellationToken cancellationToken) => throw Unavailable();

    public Task<Uri> IssueFinalReadLinkAsync(string blobName, TimeSpan lifetime, CancellationToken cancellationToken) => throw Unavailable();

    public Task<byte[]?> ReadQuarantineAsync(string blobName, int maxBytes, CancellationToken cancellationToken) => throw Unavailable();

    public Task<byte[]?> ReadFinalAsync(string blobName, CancellationToken cancellationToken) => throw Unavailable();

    public Task<bool> WriteFinalAsync(string blobName, byte[] content, CancellationToken cancellationToken) => throw Unavailable();

    /// <summary>Sin almacenamiento configurado nada pudo escribirse: borrar no hace nada. Fallar aquí bloquearía la purga (spec 057) y la baja de cuenta (spec 039) sin motivo.</summary>
    public Task DeleteQuarantineAsync(string blobName, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DeleteFinalAsync(string blobName, CancellationToken cancellationToken) => Task.CompletedTask;
}
