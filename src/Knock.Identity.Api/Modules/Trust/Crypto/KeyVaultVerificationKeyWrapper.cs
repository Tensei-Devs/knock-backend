using System.Collections.Concurrent;
using System.Security.Cryptography;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;
using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Trust.Domain;

namespace Knock.Identity.Api.Modules.Trust.Crypto;

/// <summary>
/// <see cref="IVerificationKeyWrapper"/> real (spec 025 R-11): envuelve y desenvuelve la llave de datos de cada verificación con la llave RSA <c>curp-kek</c> de Key Vault
/// (RSA-OAEP-256) usando la identidad administrada del servicio. La llave maestra no sale nunca del vault. Se enciende con <c>Verificacion:KeyVault:Habilitado</c>.
/// Cualquier fallo de Key Vault (permiso, red, llave deshabilitada) responde 503 sin repetir contenido, llaves ni identificadores.
/// </summary>
internal sealed class KeyVaultVerificationKeyWrapper(KeyVaultVerificationOptions options, Func<Uri, CryptographyClient> clientFactory) : IVerificationKeyWrapper
{
    private readonly ConcurrentDictionary<string, CryptographyClient> _clients = new(StringComparer.Ordinal);

    public static KeyVaultVerificationKeyWrapper Create(KeyVaultVerificationOptions options, TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(credential);
        return new KeyVaultVerificationKeyWrapper(options, keyId => new CryptographyClient(keyId, credential));
    }

    public async Task<WrappedVerificationKey> WrapAsync(byte[] dataKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataKey);
        try
        {
            var result = await Client(options.KekVersion).WrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, dataKey, cancellationToken).ConfigureAwait(false);
            return new WrappedVerificationKey(Convert.ToBase64String(result.EncryptedKey), options.KekVersion);
        }
        catch (Exception ex) when (IsVaultFailure(ex))
        {
            throw Unavailable();
        }
    }

    public async Task<byte[]> UnwrapAsync(WrappedVerificationKey wrapped, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(wrapped);
        if (!KeyVaultVerificationOptions.IsValidVersion(wrapped.KekVersion))
        {
            throw Unavailable();
        }

        try
        {
            var result = await Client(wrapped.KekVersion).UnwrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, Convert.FromBase64String(wrapped.Value), cancellationToken).ConfigureAwait(false);
            return result.Key;
        }
        catch (Exception ex) when (IsVaultFailure(ex) || ex is FormatException)
        {
            throw Unavailable();
        }
    }

    private static bool IsVaultFailure(Exception ex) =>
        ex is RequestFailedException or AuthenticationFailedException or CredentialUnavailableException or HttpRequestException or CryptographicException;

    private static DomainException Unavailable() =>
        new(DomainErrorKind.ServiceUnavailable, "The verification master key is unavailable.", VerificationErrors.StorageUnavailableCode);

    private CryptographyClient Client(string version) => _clients.GetOrAdd(version, v => clientFactory(options.KeyId(v)));
}
