using Microsoft.Extensions.Options;

namespace Knock.Identity.Api.Modules.Trust.Crypto;

/// <summary>
/// Llave maestra de la verificación en Key Vault (sección <c>Verificacion:KeyVault</c>, spec 025 R-11). <c>Habilitado</c> es el interruptor y viene apagado: sin él queda
/// <see cref="UnavailableVerificationKeyWrapper"/>, que falla cerrado. Nada de esto es secreto (el vault, el nombre y la versión de la llave son identificadores).
/// </summary>
internal sealed class KeyVaultVerificationOptions
{
    public const string SectionName = "Verificacion:KeyVault";

    public bool Habilitado { get; set; }

    /// <summary>Dirección del vault, por ejemplo <c>https://kv-knockdevtn01.vault.azure.net</c>.</summary>
    public string VaultUri { get; set; } = string.Empty;

    public string KeyName { get; set; } = "curp-kek";

    /// <summary>Versión de la llave con la que se envuelve lo nuevo (32 caracteres hexadecimales). Lo ya guardado se desenvuelve con la versión que trae.</summary>
    public string KekVersion { get; set; } = string.Empty;

    internal static bool IsValidVersion(string? version) =>
        version is { Length: 32 } && version.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal Uri KeyId(string version) => new($"{VaultUri.TrimEnd('/')}/keys/{KeyName}/{version}");
}

internal sealed class KeyVaultVerificationOptionsValidator : IValidateOptions<KeyVaultVerificationOptions>
{
    public ValidateOptionsResult Validate(string? name, KeyVaultVerificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Habilitado)
        {
            return ValidateOptionsResult.Success;
        }

        if (!Uri.TryCreate(options.VaultUri, UriKind.Absolute, out var vault) || vault.Scheme != Uri.UriSchemeHttps)
        {
            return ValidateOptionsResult.Fail("Verificacion:KeyVault:VaultUri must be an absolute https address when the switch is on.");
        }

        if (string.IsNullOrWhiteSpace(options.KeyName) || options.KeyName.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
        {
            return ValidateOptionsResult.Fail("Verificacion:KeyVault:KeyName is required and may only have letters, digits and hyphens.");
        }

        return KeyVaultVerificationOptions.IsValidVersion(options.KekVersion)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Verificacion:KeyVault:KekVersion must be the 32 hexadecimal characters of the key version.");
    }
}
