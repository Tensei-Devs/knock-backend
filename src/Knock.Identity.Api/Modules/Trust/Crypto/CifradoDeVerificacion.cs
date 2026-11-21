using System.Security.Cryptography;
using System.Text;

namespace Knock.Identity.Api.Modules.Trust.Crypto;

/// <summary>Llave de datos envuelta con la llave maestra: es lo único que se guarda en <c>verificaciones</c> (<c>llaveEnvuelta</c> y <c>llaveKekVersion</c>).</summary>
internal sealed record WrappedVerificationKey(string Value, string KekVersion);

/// <summary>
/// Envuelve y desenvuelve la llave de datos con la llave maestra de Key Vault (spec 025, R-11). La implementación real, que llama a Key Vault y nunca saca la
/// maestra, es de la tarea T-04 y de la infraestructura; las pruebas usan un doble que envuelve igual.
/// </summary>
internal interface IVerificationKeyWrapper
{
    Task<WrappedVerificationKey> WrapAsync(byte[] dataKey, CancellationToken cancellationToken);

    Task<byte[]> UnwrapAsync(WrappedVerificationKey wrapped, CancellationToken cancellationToken);
}

/// <summary>Llave de datos de 256 bits de una verificación. Se pone en ceros al desecharla.</summary>
internal sealed class VerificationDataKey : IDisposable
{
    public const int KeyBytes = 32;

    internal VerificationDataKey(byte[] key)
    {
        if (key is null || key.Length != KeyBytes)
        {
            throw new ArgumentException("The verification data key must have 32 bytes.", nameof(key));
        }

        Bytes = key;
    }

    internal byte[] Bytes { get; }

    public void Dispose() => CryptographicOperations.ZeroMemory(Bytes);
}

/// <summary>
/// AES-256-GCM con la llave de cada verificación (spec 025, R-10 y R-11). El envoltorio del archivo es <c>"KVF1" ‖ nonce (12) ‖ etiqueta (16) ‖ cifrado</c>: el nonce
/// y la etiqueta viajan con el blob y nunca en la base. El <c>context</c> (por ejemplo <c>{verificacionId}|{tipo}</c>) es dato asociado autenticado: un blob copiado a otra
/// verificación o tipo de foto no se descifra. Ningún error incluye el contenido, el contexto ni las llaves.
/// </summary>
internal sealed class CifradoDeVerificacion(IVerificationKeyWrapper wrapper)
{
    private const int _nonceBytes = 12;
    private const int _tagBytes = 16;
    private static readonly byte[] _magic = [(byte)'K', (byte)'V', (byte)'F', (byte)'1'];
    private static readonly int _headerBytes = _magic.Length + _nonceBytes + _tagBytes;

    /// <summary>Genera una llave de datos nueva y la devuelve junto con su versión envuelta, que es la que se guarda.</summary>
    public async Task<(VerificationDataKey Key, WrappedVerificationKey Wrapped)> CreateKeyAsync(CancellationToken cancellationToken)
    {
        var key = new VerificationDataKey(RandomNumberGenerator.GetBytes(VerificationDataKey.KeyBytes));
        try
        {
            var wrapped = await wrapper.WrapAsync(key.Bytes, cancellationToken).ConfigureAwait(false);
            return (key, wrapped);
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    /// <summary>Recupera la llave de datos desde su versión envuelta.</summary>
    public async Task<VerificationDataKey> UnwrapKeyAsync(WrappedVerificationKey wrapped, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(wrapped);
        return new VerificationDataKey(await wrapper.UnwrapAsync(wrapped, cancellationToken).ConfigureAwait(false));
    }

    public static byte[] Encrypt(VerificationDataKey key, ReadOnlySpan<byte> content, string context)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrEmpty(context);
        var packed = new byte[_headerBytes + content.Length];
        _magic.CopyTo(packed, 0);
        var nonce = packed.AsSpan(_magic.Length, _nonceBytes);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key.Bytes, _tagBytes);
        aes.Encrypt(nonce, content, packed.AsSpan(_headerBytes), packed.AsSpan(_magic.Length + _nonceBytes, _tagBytes), Encoding.UTF8.GetBytes(context));
        return packed;
    }

    public static byte[] Decrypt(VerificationDataKey key, ReadOnlySpan<byte> envelope, string context)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrEmpty(context);
        if (envelope.Length < _headerBytes || !envelope[.._magic.Length].SequenceEqual(_magic))
        {
            throw new CryptographicException("The encrypted file has an unknown format.");
        }

        var plain = new byte[envelope.Length - _headerBytes];
        using var aes = new AesGcm(key.Bytes, _tagBytes);
        aes.Decrypt(
            envelope.Slice(_magic.Length, _nonceBytes),
            envelope[_headerBytes..],
            envelope.Slice(_magic.Length + _nonceBytes, _tagBytes),
            plain,
            Encoding.UTF8.GetBytes(context));
        return plain;
    }
}
