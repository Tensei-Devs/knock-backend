using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Modules.Moderation.Services;

/// <summary>Modo de operación (spec 031, R-15): <c>azure</c> llama a Content Safety; <c>solo_local</c> aprueba lo que pasa la lista local y solo existe en desarrollo.</summary>
internal static class ModerationModes
{
    public const string Azure = "azure";
    public const string LocalOnly = "solo_local";
}

/// <summary>Sección <c>Moderacion</c> (spec 031, §5). La credencial no es configuración del repositorio: identidad administrada o Key Vault.</summary>
internal sealed class ModerationOptions
{
    public const string SectionName = "Moderacion";
    public const string ServiceName = "market";

    /// <summary>
    /// Interruptor del gasto en Azure. Apagado por omisión: la decisión de gasto es de una persona (spec 031, §15). Apagado, el moderador solo puede rechazar con la lista local y lo
    /// demás es <c>Unavailable</c> (falla cerrada), que es lo que ya hacía el servicio antes de esta spec.
    /// </summary>
    public bool Habilitado { get; set; }

    public string Modo { get; set; } = ModerationModes.Azure;

    public ContentSafetySection ContentSafety { get; set; } = new();

    [Range(1, 30)]
    public int TiempoMaximoSegundos { get; set; } = 5;

    public ThresholdSection Umbrales { get; set; } = new();

    /// <summary>Versión de los umbrales: <c>&lt;etiqueta&gt;-&lt;huella de 8 hex&gt;</c>. La huella se calcula de los umbrales, de modo que cambiar uno sin subir la versión impide arrancar (R-19).</summary>
    public string VersionUmbrales { get; set; } = "1-" + ThresholdSection.Default.Fingerprint();

    public LimitSection Limites { get; set; } = new();

    /// <summary>Versión de la API de Content Safety; se fija en configuración y en <c>Memoria.md</c>.</summary>
    public string VersionApi { get; set; } = "2024-09-01";
}

internal sealed class ContentSafetySection
{
    public string? Endpoint { get; set; }

    /// <summary>Opcional: si se usa clave sale de Key Vault por configuración. Sin clave se usa la identidad administrada.</summary>
    public string? Clave { get; set; }
}

internal sealed class ThresholdSection
{
    public static readonly ThresholdSection Default = new();

    public int Hate { get; set; } = 4;

    public int Sexual { get; set; } = 2;

    public int Violence { get; set; } = 4;

    public int SelfHarm { get; set; } = 4;

    public IReadOnlyDictionary<string, int> AsMap() => new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["Hate"] = Hate,
        ["Sexual"] = Sexual,
        ["Violence"] = Violence,
        ["SelfHarm"] = SelfHarm,
    };

    /// <summary>Huella corta y estable de los umbrales (no es un secreto: solo detecta un cambio sin subir la versión).</summary>
    public string Fingerprint()
    {
        var text = string.Create(CultureInfo.InvariantCulture, $"{Hate}|{Sexual}|{Violence}|{SelfHarm}");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..8];
    }
}

internal sealed class LimitSection
{
    public int LlamadasPorMes { get; set; } = 4000;

    public int LlamadasPorPersonaPorDia { get; set; } = 300;
}

/// <summary>Valida al arrancar modo, entorno, punto de conexión, umbrales, versión y límites (spec 031, R-15 y R-19). Los mensajes nombran la clave, nunca el valor.</summary>
internal sealed class ModerationOptionsValidator(IHostEnvironment environment) : IValidateOptions<ModerationOptions>
{
    public ValidateOptionsResult Validate(string? name, ModerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (options.Modo is not (ModerationModes.Azure or ModerationModes.LocalOnly))
        {
            failures.Add("Moderacion:Modo must be 'azure' or 'solo_local'.");
        }
        else
        {
            CheckMode(options, failures);
        }

        foreach (var (category, value) in options.Umbrales.AsMap())
        {
            if (value is not (2 or 4 or 6))
            {
                failures.Add($"Moderacion:Umbrales:{category} must be 2, 4 or 6.");
            }
        }

        if (!string.Equals(options.VersionUmbrales.Split('-').LastOrDefault(), options.Umbrales.Fingerprint(), StringComparison.Ordinal))
        {
            failures.Add("Moderacion:VersionUmbrales must end with the fingerprint of the thresholds ('<label>-<fingerprint>'): bump it when a threshold changes.");
        }

        if (options.TiempoMaximoSegundos is not (>= 1 and <= 30))
        {
            failures.Add("Moderacion:TiempoMaximoSegundos must be from 1 to 30.");
        }

        if (options.Limites.LlamadasPorMes < 1 || options.Limites.LlamadasPorPersonaPorDia < 1)
        {
            failures.Add("Moderacion:Limites values must be positive.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private void CheckMode(ModerationOptions options, List<string> failures)
    {
        if (options.Modo == ModerationModes.LocalOnly && !environment.IsDevelopment())
        {
            failures.Add("Moderacion:Modo 'solo_local' is only allowed when ASPNETCORE_ENVIRONMENT is Development.");
        }

        if (options.Habilitado && options.Modo == ModerationModes.Azure
            && (!Uri.TryCreate(options.ContentSafety.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add("Moderacion:ContentSafety:Endpoint must be an absolute https URL when Modo is 'azure'.");
        }
    }
}
