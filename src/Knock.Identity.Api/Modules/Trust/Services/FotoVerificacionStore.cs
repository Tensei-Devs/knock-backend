using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Trust.Crypto;
using Knock.Identity.Api.Modules.Trust.Domain;
using Knock.Identity.Api.Modules.Trust.Storage;
using MongoDB.Bson;

namespace Knock.Identity.Api.Modules.Trust.Services;

/// <summary>Un enlace de una foto: de subida a cuarentena (R-08) o de lectura del blob cifrado definitivo.</summary>
internal sealed record PhotoLink(string Type, Uri Url, DateTime ExpiresAt);

/// <summary>
/// Único punto del módulo con las fotos y Key Vault (spec 025, T-04). SEGURIDAD Y CRIPTOGRAFÍA: valida por CONTENIDO REAL (firma JPG/PNG y tamaño), cifra con la llave de la
/// verificación, escribe solo cifrado en el contenedor definitivo y borra la cuarentena; ningún error ni registro lleva el contenido, los nombres de blob ni los enlaces.
/// </summary>
internal sealed class FotoVerificacionStore(IVerificationBlobStore blobs, CifradoDeVerificacion crypto, TimeProvider clock)
{
    /// <summary>Tope de una foto: 8 MB (R-10).</summary>
    public const int MaxPhotoBytes = 8 * 1024 * 1024;

    /// <summary>SAS de 5 minutos (R-08).</summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(5);

    private static readonly byte[] _pngSignature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] _jpegSignature = [0xFF, 0xD8, 0xFF];

    /// <summary>Ruta interna de un blob (nunca pública): <c>{verificacionId}/{tipo}</c>. El tipo ya es uno del catálogo.</summary>
    public static string BlobName(ObjectId verificationId, string type) => $"{verificationId}/{type}";

    /// <summary>Contexto autenticado del cifrado: un blob copiado a otra verificación o tipo no se descifra.</summary>
    public static string ContextOf(ObjectId verificationId, string type) => $"{verificationId}|{type}";

    /// <summary>JPG (FFD8FF) o PNG (firma de 8 bytes), no vacío y de 8 MB o menos. No confía en la extensión ni en el tipo declarado.</summary>
    public static bool IsAcceptedImage(ReadOnlySpan<byte> content) =>
        content.Length is > 0 and <= MaxPhotoBytes && (content.StartsWith(_jpegSignature) || content.StartsWith(_pngSignature));

    public Task<IReadOnlyList<PhotoLink>> IssueUploadLinksAsync(ObjectId verificationId, IEnumerable<string> types, CancellationToken cancellationToken) =>
        IssueAsync(verificationId, types, LinkLifetime, blobs.IssueQuarantineUploadLinkAsync, cancellationToken);

    /// <summary>
    /// R-10 y R-12: descarga la cuarentena, valida el contenido, cifra, guarda el definitivo y borra la cuarentena.
    /// 409 <c>photo_not_uploaded</c> si no hay blob; 409 <c>photo_already_confirmed</c> si el definitivo ya existe; 422 <c>content_invalid</c> (y borra la cuarentena) si no es JPG/PNG o pesa más de 8 MB.
    /// </summary>
    public async Task ConfirmAsync(ObjectId verificationId, string type, WrappedVerificationKey wrappedKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(wrappedKey);
        var name = BlobName(verificationId, type);
        var uploaded = await blobs.ReadQuarantineAsync(name, MaxPhotoBytes, cancellationToken);
        if (uploaded is null)
        {
            throw new DomainException(DomainErrorKind.Conflict, "The photo was not uploaded.", VerificationErrors.PhotoNotUploadedCode);
        }

        if (!IsAcceptedImage(uploaded))
        {
            await blobs.DeleteQuarantineAsync(name, cancellationToken);
            throw new DomainException(DomainErrorKind.Unprocessable, "The photo content is not valid.", VerificationErrors.ContentInvalidCode);
        }

        using var key = await crypto.UnwrapKeyAsync(wrappedKey, cancellationToken);
        var envelope = CifradoDeVerificacion.Encrypt(key, uploaded, ContextOf(verificationId, type));
        if (!await blobs.WriteFinalAsync(name, envelope, cancellationToken))
        {
            throw new DomainException(DomainErrorKind.Conflict, "The photo was already confirmed.", VerificationErrors.PhotoAlreadyConfirmedCode);
        }

        await blobs.DeleteQuarantineAsync(name, cancellationToken);
    }

    /// <summary>Descifra una foto guardada (para el panel de revisión, HU-067). <see langword="null"/> si no existe.</summary>
    public async Task<byte[]?> ReadDecryptedAsync(ObjectId verificationId, string type, WrappedVerificationKey wrappedKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(wrappedKey);
        var envelope = await blobs.ReadFinalAsync(BlobName(verificationId, type), cancellationToken);
        if (envelope is null)
        {
            return null;
        }

        using var key = await crypto.UnwrapKeyAsync(wrappedKey, cancellationToken);
        return CifradoDeVerificacion.Decrypt(key, envelope, ContextOf(verificationId, type));
    }

    /// <summary>Enlaces de lectura de los blobs definitivos (siguen cifrados). Los enlaces no se guardan, no se cachean y no se registran.</summary>
    public Task<IReadOnlyList<PhotoLink>> IssueReadLinksAsync(ObjectId verificationId, IEnumerable<string> types, TimeSpan lifetime, CancellationToken cancellationToken) =>
        IssueAsync(verificationId, types, lifetime, blobs.IssueFinalReadLinkAsync, cancellationToken);

    /// <summary>R-16 y R-17: borra cuarentena y definitivo de todos los tipos; idempotente.</summary>
    public async Task PurgeAsync(ObjectId verificationId, IEnumerable<string> types, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(types);
        foreach (var type in types)
        {
            var name = BlobName(verificationId, type);
            await blobs.DeleteQuarantineAsync(name, cancellationToken);
            await blobs.DeleteFinalAsync(name, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<PhotoLink>> IssueAsync(
        ObjectId verificationId,
        IEnumerable<string> types,
        TimeSpan lifetime,
        Func<string, TimeSpan, CancellationToken, Task<Uri>> issue,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(types);
        var expires = clock.GetUtcNow().UtcDateTime + lifetime;
        var links = new List<PhotoLink>();
        foreach (var type in types)
        {
            links.Add(new PhotoLink(type, await issue(BlobName(verificationId, type), lifetime, cancellationToken), expires));
        }

        return links;
    }
}
