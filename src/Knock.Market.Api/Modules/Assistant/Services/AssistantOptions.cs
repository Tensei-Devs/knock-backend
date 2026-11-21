using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Modules.Assistant.Services;

/// <summary>Sección <c>Asistente</c> (spec 024, §5). La llave no es configuración del repositorio: identidad administrada o Key Vault.</summary>
internal sealed class AssistantOptions
{
    public const string SectionName = "Asistente";

    /// <summary>Interruptor del gasto: falso por omisión (R-09). Solo una persona lo enciende al aprobar el tope de KNOCK-63.</summary>
    public bool Habilitado { get; set; }

    public AzureOpenAiSection AzureOpenAI { get; set; } = new();

    public AssistantLimits Limites { get; set; } = new();
}

internal sealed class AzureOpenAiSection
{
    public string? Endpoint { get; set; }

    public string? ModeloDespliegue { get; set; }

    /// <summary>Versión de la API de Azure OpenAI; se fija aquí y en <c>Memoria.md</c>.</summary>
    public string VersionApi { get; set; } = "2025-04-01-preview";

    /// <summary>Opcional: si se usa clave sale de Key Vault por configuración. Sin clave se usa la identidad administrada.</summary>
    public string? Clave { get; set; }
}

internal sealed class AssistantLimits
{
    public int TokensPorUsuarioPorDia { get; set; } = 20_000;

    public int TokensGlobalesPorDia { get; set; } = 200_000;

    public int TiempoMaximoSegundos { get; set; } = 10;

    /// <summary>Tope de tokens de salida por llamada; la reserva de consumo usa la entrada estimada más este valor (PARA VALIDACIÓN DE JOSÉ, KNOCK-63 T-01).</summary>
    public int TokensSalidaMaximos { get; set; } = 800;
}

/// <summary>Valida al arrancar; los mensajes nombran la clave, nunca el valor.</summary>
internal sealed class AssistantOptionsValidator : IValidateOptions<AssistantOptions>
{
    public ValidateOptionsResult Validate(string? name, AssistantOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (options.Limites.TokensPorUsuarioPorDia < 1 || options.Limites.TokensGlobalesPorDia < options.Limites.TokensPorUsuarioPorDia)
        {
            failures.Add("Asistente:Limites token limits must be positive and the global limit must not be below the per-user limit.");
        }

        if (options.Limites.TiempoMaximoSegundos is < 1 or > 60 || options.Limites.TokensSalidaMaximos is < 50 or > 4000)
        {
            failures.Add("Asistente:Limites timeout (1 to 60 s) or output tokens (50 to 4000) are out of range.");
        }

        if (options.Habilitado && (!Uri.TryCreate(options.AzureOpenAI.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(options.AzureOpenAI.ModeloDespliegue)))
        {
            failures.Add("Asistente:AzureOpenAI:Endpoint (https) and ModeloDespliegue are required when Asistente:Habilitado is true.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
