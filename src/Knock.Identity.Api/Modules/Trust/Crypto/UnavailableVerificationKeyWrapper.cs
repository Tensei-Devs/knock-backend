using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Trust.Domain;

namespace Knock.Identity.Api.Modules.Trust.Crypto;

/// <summary>
/// Valor por omisión de <see cref="IVerificationKeyWrapper"/>: sin la llave maestra de Key Vault no se cifra ni se descifra nada, así que falla cerrado con 503. El cliente real
/// (Key Vault, <c>WrapKey</c>/<c>UnwrapKey</c> con identidad administrada) se registra antes de <c>AddTrustModule()</c> y reemplaza a este.
/// </summary>
internal sealed class UnavailableVerificationKeyWrapper : IVerificationKeyWrapper
{
    private static DomainException Unavailable() =>
        new(DomainErrorKind.ServiceUnavailable, "The verification master key is not configured.", VerificationErrors.StorageUnavailableCode);

    public Task<WrappedVerificationKey> WrapAsync(byte[] dataKey, CancellationToken cancellationToken) => throw Unavailable();

    public Task<byte[]> UnwrapAsync(WrappedVerificationKey wrapped, CancellationToken cancellationToken) => throw Unavailable();
}
