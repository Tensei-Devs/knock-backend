using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Knock.Identity.Api.Modules.Trust.Domain;

namespace Knock.Identity.Api.Modules.Trust.Crypto;

/// <summary>
/// Llaves HMAC de la CURP por versión (spec 025, R-05): cada una de al menos 32 bytes y <see cref="CurrentVersion"/> es la que firma; las demás solo sirven
/// para buscar duplicados después de una rotación. Las llaves salen de Key Vault en el arranque; el repositorio no trae ninguna.
/// </summary>
internal sealed class CurpHmacKeys
{
    public const int MinimumKeyBytes = 32;

    private readonly Dictionary<int, byte[]> _keys;

    public CurpHmacKeys(IReadOnlyDictionary<int, byte[]> keys, int currentVersion)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (!keys.ContainsKey(currentVersion))
        {
            throw new ArgumentException("The current CURP HMAC key version is not among the keys.", nameof(currentVersion));
        }

        _keys = [];
        foreach (var (version, key) in keys)
        {
            ArgumentNullException.ThrowIfNull(key);
            if (key.Length < MinimumKeyBytes)
            {
                throw new ArgumentException("Every CURP HMAC key must have at least 32 bytes.", nameof(keys));
            }

            _keys[version] = (byte[])key.Clone();
        }

        CurrentVersion = currentVersion;
    }

    public int CurrentVersion { get; }

    public IEnumerable<int> Versions => _keys.Keys;

    internal byte[] KeyOf(int version) => _keys[version];
}

/// <summary>
/// HMAC-SHA256 de la CURP normalizada (mayúsculas, sin espacios a los lados). El valor guardado es <c>{versión}.{hex en minúsculas}</c>: la versión permite rotar la
/// llave sin perder el índice de duplicados (se busca con <see cref="ComputeAll"/>). Nunca devuelve ni registra la CURP, y los mensajes de error no la incluyen.
/// </summary>
internal sealed class CurpHmac(CurpHmacKeys keys)
{
    private const char _separator = '.';

    /// <summary>HMAC con la llave vigente.</summary>
    public string Compute(string curp) => ComputeWith(curp, keys.CurrentVersion);

    /// <summary>HMAC de la misma CURP con cada versión de llave configurada, para buscar duplicados guardados con una llave anterior.</summary>
    public IReadOnlyList<string> ComputeAll(string curp) => [.. keys.Versions.Select(version => ComputeWith(curp, version))];

    /// <summary>Compara en tiempo constante la CURP con un valor guardado; falso si la versión del valor no existe o el formato no es el esperado.</summary>
    public bool Matches(string curp, string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        var split = stored.IndexOf(_separator, StringComparison.Ordinal);
        if (split <= 0 || !int.TryParse(stored.AsSpan(0, split), NumberStyles.None, CultureInfo.InvariantCulture, out var version) || !keys.Versions.Contains(version))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(ComputeWith(curp, version)),
            Encoding.ASCII.GetBytes(stored));
    }

    private string ComputeWith(string curp, int version)
    {
        if (!Curp.IsValid(curp))
        {
            throw new ArgumentException("The CURP does not have a valid format.", nameof(curp));
        }

        var data = Encoding.ASCII.GetBytes(curp.Trim().ToUpperInvariant());
        try
        {
            var mac = HMACSHA256.HashData(keys.KeyOf(version), data);
            return string.Create(CultureInfo.InvariantCulture, $"{version}{_separator}{Convert.ToHexStringLower(mac)}");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(data);
        }
    }
}
