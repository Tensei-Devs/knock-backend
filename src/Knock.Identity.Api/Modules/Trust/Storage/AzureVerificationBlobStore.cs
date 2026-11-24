using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Trust.Domain;

namespace Knock.Identity.Api.Modules.Trust.Storage;

/// <summary>
/// <see cref="IVerificationBlobStore"/> real sobre la cuenta privada de verificación (spec 025, T-04, R-08, R-10, R-12). Los SAS son de delegación de usuario firmados con la identidad
/// administrada (nunca la llave de cuenta): de ESCRITURA de un solo blob de cuarentena y de LECTURA de un solo blob definitivo, solo HTTPS, de corta vida. El contenido definitivo ya llega cifrado.
/// Cualquier fallo de Azure responde 503 sin repetir nombres de blob, enlaces ni contenido. Se enciende con <c>Verificacion:Blob:Habilitado</c> (apagado por omisión).
/// </summary>
internal sealed class AzureVerificationBlobStore(BlobServiceClient service, VerificationBlobOptions options, TimeProvider clock) : IVerificationBlobStore, IDisposable
{
    /// <summary>Tolerancia de reloj: el SAS empieza un minuto antes de emitirse.</summary>
    internal static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan _keyLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan _keyMargin = TimeSpan.FromHours(1);

    private readonly BlobContainerClient _quarantine = service.GetBlobContainerClient(options.ContenedorCuarentena);
    private readonly BlobContainerClient _final = service.GetBlobContainerClient(options.ContenedorDefinitivo);
    private readonly SemaphoreSlim _keyGate = new(1, 1);
    private UserDelegationKey? _key;

    public static AzureVerificationBlobStore Create(VerificationBlobOptions options, TokenCredential credential, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(credential);
        return new AzureVerificationBlobStore(new BlobServiceClient(new Uri(options.CuentaUrl), credential), options, clock);
    }

    public Task<Uri> IssueQuarantineUploadLinkAsync(string blobName, TimeSpan lifetime, CancellationToken cancellationToken) =>
        Guard(async () => Sign(_quarantine.GetBlobClient(blobName), await KeyAsync(cancellationToken), BlobSasPermissions.Create | BlobSasPermissions.Write, clock.GetUtcNow(), lifetime));

    public Task<Uri> IssueFinalReadLinkAsync(string blobName, TimeSpan lifetime, CancellationToken cancellationToken) =>
        Guard(async () => Sign(_final.GetBlobClient(blobName), await KeyAsync(cancellationToken), BlobSasPermissions.Read, clock.GetUtcNow(), lifetime));

    public Task<byte[]?> ReadQuarantineAsync(string blobName, int maxBytes, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 1);
        return Guard(async () =>
        {
            try
            {
                var response = await _quarantine.GetBlobClient(blobName).DownloadStreamingAsync(new BlobDownloadOptions { Range = new HttpRange(0, (long)maxBytes + 1) }, cancellationToken);
                using var download = response.Value;
                return (byte[]?)await ReadAtMostAsync(download.Content, (long)maxBytes + 1, cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
            catch (RequestFailedException ex) when (ex.Status == 416)
            {
                return [];
            }
        });
    }

    public Task<byte[]?> ReadFinalAsync(string blobName, CancellationToken cancellationToken) =>
        Guard(async () =>
        {
            try
            {
                var response = await _final.GetBlobClient(blobName).DownloadContentAsync(cancellationToken);
                return (byte[]?)response.Value.Content.ToArray();
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        });

    public Task<bool> WriteFinalAsync(string blobName, byte[] content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Guard(async () =>
        {
            var upload = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/octet-stream" },
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
            };
            try
            {
                await _final.GetBlobClient(blobName).UploadAsync(BinaryData.FromBytes(content), upload, cancellationToken);
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status is 409 or 412)
            {
                return false;
            }
        });
    }

    public Task DeleteQuarantineAsync(string blobName, CancellationToken cancellationToken) =>
        Guard(async () => (await _quarantine.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken)).Value);

    public Task DeleteFinalAsync(string blobName, CancellationToken cancellationToken) =>
        Guard(async () => (await _final.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken)).Value);

    public void Dispose() => _keyGate.Dispose();

    /// <summary>Firma un solo blob con la llave de delegación: HTTPS, de un minuto antes a <paramref name="lifetime"/> después de <paramref name="now"/>. Estática para probarse sin red.</summary>
    internal static Uri Sign(BlobClient blob, UserDelegationKey key, BlobSasPermissions permissions, DateTimeOffset now, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(blob);
        ArgumentNullException.ThrowIfNull(key);
        var sas = new BlobSasBuilder(permissions, now + lifetime)
        {
            BlobContainerName = blob.BlobContainerName,
            BlobName = blob.Name,
            Resource = "b",
            Protocol = SasProtocol.Https,
            StartsOn = now - ClockSkew,
        };
        return new BlobUriBuilder(blob.Uri) { Sas = sas.ToSasQueryParameters(key, blob.AccountName) }.ToUri();
    }

    private async Task<UserDelegationKey> KeyAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (_key is { } current && current.SignedExpiresOn - now > _keyMargin)
        {
            return current;
        }

        await _keyGate.WaitAsync(cancellationToken);
        try
        {
            if (_key is { } cached && cached.SignedExpiresOn - now > _keyMargin)
            {
                return cached;
            }

            var response = await service.GetUserDelegationKeyAsync(now - ClockSkew, now + _keyLifetime, cancellationToken);
            _key = response.Value;
            return _key;
        }
        finally
        {
            _keyGate.Release();
        }
    }

    private static async Task<byte[]> ReadAtMostAsync(Stream content, long limit, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while (buffer.Length < limit && (read = await content.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, limit - buffer.Length)), cancellationToken)) > 0)
        {
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return buffer.ToArray();
    }

    private static async Task<T> Guard<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (ex is RequestFailedException or AuthenticationFailedException or CredentialUnavailableException or HttpRequestException)
        {
            throw new DomainException(DomainErrorKind.ServiceUnavailable, "The private verification storage is unavailable.", VerificationErrors.StorageUnavailableCode);
        }
    }
}
