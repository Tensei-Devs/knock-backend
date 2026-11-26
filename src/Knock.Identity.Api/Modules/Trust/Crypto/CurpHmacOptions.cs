using Microsoft.Extensions.Options;

namespace Knock.Identity.Api.Modules.Trust.Crypto;

/// <summary>
/// Llaves HMAC de la CURP (sección <c>Verificacion:CurpHmac</c>): <c>Keys</c> mapea la versión a Base64 de 32 bytes o más y <c>ActiveVersion</c> es la que firma. Los
/// valores llegan de Key Vault por configuración; el repositorio no trae ninguno. Si faltan, el HMAC falla al usarse (nunca guarda la CURP) y el resto de Identidad arranca.
/// </summary>
internal sealed class CurpHmacOptions
{
    public const string SectionName = "Verificacion:CurpHmac";

    public int ActiveVersion { get; set; }

    public Dictionary<int, string> Keys { get; } = [];

    internal static CurpHmacKeys ToKeys(CurpHmacOptions options)
    {
        var decoded = new Dictionary<int, byte[]>();
        foreach (var (version, encoded) in options.Keys)
        {
            var buffer = new byte[(encoded.Length * 3 / 4) + 3];
            if (!Convert.TryFromBase64String(encoded, buffer, out var written))
            {
                throw new InvalidOperationException("Every CURP HMAC key must be Base64.");
            }

            decoded[version] = buffer[..written];
        }

        if (decoded.Count == 0)
        {
            throw new InvalidOperationException("The CURP HMAC keys are not configured.");
        }

        return new CurpHmacKeys(decoded, options.ActiveVersion);
    }

    internal static CurpHmac Create(IOptions<CurpHmacOptions> options) => new(() => ToKeys(options.Value));
}
