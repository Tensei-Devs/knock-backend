using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Knock.Messaging.Api.Modules.Avisos.Services;

internal sealed class TokenProtection
{
    private const int _nonceSize = 12;
    private const int _tagSize = 16;
    private readonly string _version;
    private readonly Dictionary<string, byte[]> _encryptionKeys;
    private readonly byte[] _indexKey;

    public TokenProtection(string version, byte[] encryptionKey, byte[] indexKey)
        : this(version, new Dictionary<string, byte[]> { [version] = encryptionKey }, indexKey)
    {
    }

    public TokenProtection(string version, Dictionary<string, byte[]> encryptionKeys, byte[] indexKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(encryptionKeys);
        ArgumentNullException.ThrowIfNull(indexKey);
        if (indexKey.Length != 32 || !encryptionKeys.ContainsKey(version)
            || encryptionKeys.Any(entry => string.IsNullOrWhiteSpace(entry.Key) || entry.Key.Contains('.', StringComparison.Ordinal)
                || entry.Value is null || entry.Value.Length != 32 || CryptographicOperations.FixedTimeEquals(entry.Value, indexKey)))
        {
            throw new ArgumentException("Invalid token protection configuration.");
        }

        _version = version;
        _encryptionKeys = encryptionKeys.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.Ordinal);
        _indexKey = indexKey.ToArray();
    }

    public string Fingerprint(string token) =>
        Convert.ToHexString(HMACSHA256.HashData(_indexKey, Encoding.UTF8.GetBytes(token)));

    public string Encrypt(string token, string owner, string id)
    {
        var data = Encoding.UTF8.GetBytes(token);
        var packed = new byte[_nonceSize + _tagSize + data.Length];
        RandomNumberGenerator.Fill(packed.AsSpan(0, _nonceSize));
        using var aes = new AesGcm(_encryptionKeys[_version], _tagSize);
        aes.Encrypt(packed.AsSpan(0, _nonceSize), data, packed.AsSpan(_nonceSize + _tagSize), packed.AsSpan(_nonceSize, _tagSize), Context(owner, id));
        return _version + "." + WebEncoders.Base64UrlEncode(packed);
    }

    public string Decrypt(string stored, string owner, string id)
    {
        var separator = stored.IndexOf('.', StringComparison.Ordinal);
        if (separator < 1 || !_encryptionKeys.TryGetValue(stored[..separator], out var key))
        {
            throw new CryptographicException("Unknown token encryption key version.");
        }

        var packed = WebEncoders.Base64UrlDecode(stored[(separator + 1)..]);
        if (packed.Length < _nonceSize + _tagSize)
        {
            throw new CryptographicException("Invalid encrypted token.");
        }

        var plaintext = new byte[packed.Length - _nonceSize - _tagSize];
        using var aes = new AesGcm(key, _tagSize);
        aes.Decrypt(packed.AsSpan(0, _nonceSize), packed.AsSpan(_nonceSize + _tagSize), packed.AsSpan(_nonceSize, _tagSize), plaintext, Context(owner, id));
        return Encoding.UTF8.GetString(plaintext);
    }

    private static byte[] Context(string owner, string id) =>
        Encoding.UTF8.GetBytes($"{owner.Length}:{owner}{id.Length}:{id}");
}
