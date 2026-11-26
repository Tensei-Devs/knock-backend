using Microsoft.Extensions.Options;

namespace Knock.Identity.Api.Modules.Trust.Storage;

/// <summary>
/// Cuenta de almacenamiento PRIVADA de verificación (sección <c>Verificacion:Blob</c>, spec 025 R-08 y ADR-007). <c>Habilitado</c> es el interruptor y viene apagado: sin él queda
/// <see cref="UnavailableVerificationBlobStore"/>, que falla cerrado (503). Nada de esto es secreto: el acceso es con la identidad administrada, sin llaves de cuenta.
/// </summary>
internal sealed class VerificationBlobOptions
{
    public const string SectionName = "Verificacion:Blob";

    public bool Habilitado { get; set; }

    /// <summary>Dirección de la cuenta, por ejemplo <c>https://stknockverificacion.blob.core.windows.net</c> (cuenta separada de la pública, ADR-007).</summary>
    public string CuentaUrl { get; set; } = string.Empty;

    /// <summary>Contenedor privado donde la app sube con un SAS de escritura de 5 minutos.</summary>
    public string ContenedorCuarentena { get; set; } = "verificaciones-cuarentena";

    /// <summary>Contenedor privado del contenido ya cifrado.</summary>
    public string ContenedorDefinitivo { get; set; } = "verificaciones";

    internal static bool IsValidContainer(string? name) =>
        name is { Length: >= 3 and <= 63 } && name.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-') && name[0] != '-' && name[^1] != '-';
}

internal sealed class VerificationBlobOptionsValidator : IValidateOptions<VerificationBlobOptions>
{
    public ValidateOptionsResult Validate(string? name, VerificationBlobOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Habilitado)
        {
            return ValidateOptionsResult.Success;
        }

        if (!Uri.TryCreate(options.CuentaUrl, UriKind.Absolute, out var account) || account.Scheme != Uri.UriSchemeHttps)
        {
            return ValidateOptionsResult.Fail("Verificacion:Blob:CuentaUrl must be an absolute https address when the switch is on.");
        }

        if (!VerificationBlobOptions.IsValidContainer(options.ContenedorCuarentena) || !VerificationBlobOptions.IsValidContainer(options.ContenedorDefinitivo))
        {
            return ValidateOptionsResult.Fail("Verificacion:Blob container names must be 3 to 63 lowercase letters, digits or hyphens.");
        }

        return string.Equals(options.ContenedorCuarentena, options.ContenedorDefinitivo, StringComparison.Ordinal)
            ? ValidateOptionsResult.Fail("Verificacion:Blob quarantine and final containers must differ.")
            : ValidateOptionsResult.Success;
    }
}
