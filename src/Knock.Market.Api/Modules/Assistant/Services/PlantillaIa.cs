using System.Reflection;
using System.Text.Json;

namespace Knock.Market.Api.Modules.Assistant.Services;

/// <summary>Prompt versionado: <c>Prompts/&lt;funcion&gt;.v&lt;N&gt;.md</c> (spec 024, R-14). Cambiar un prompt sube la versión.</summary>
internal sealed record PromptIa(string Version, string Text);

/// <summary>
/// Plantillas locales de respaldo y prompts, incrustados en el ensamblado (spec 024, R-11 y R-14). Se leen y validan al arrancar contra el tipo cerrado de cada función, de modo que una
/// plantilla rota impide arrancar en lugar de fallar a mitad de una respuesta. Sin datos personales y en español de México.
/// </summary>
internal sealed class PlantillaIa
{
    private const string _prefix = "Assistant.";
    private const string _fence = "```";
    private readonly Dictionary<AiFunction, string> _templates = [];
    private readonly Dictionary<AiFunction, PromptIa> _prompts = [];

    private PlantillaIa()
    {
    }

    public static PlantillaIa Load()
    {
        var assembly = typeof(PlantillaIa).Assembly;
        var result = new PlantillaIa();
        foreach (var function in Enum.GetValues<AiFunction>())
        {
            var name = AiText.Name(function);
            result._templates[function] = ExtractJson(ReadResource(assembly, $"{_prefix}{name}.md"));
            var promptResource = assembly.GetManifestResourceNames().Where(resource => resource.StartsWith($"{_prefix}{name}.v", StringComparison.Ordinal))
                .OrderByDescending(resource => resource, StringComparer.Ordinal).First();
            result._prompts[function] = new PromptIa(promptResource[_prefix.Length..^".md".Length], ReadResource(assembly, promptResource));
        }

        result.Validate();
        return result;
    }

    public PromptIa Prompt(AiFunction function) => _prompts[function];

    /// <summary>Plantilla de la función como el tipo cerrado de salida; <c>null</c> si no cumple el esquema.</summary>
    public T? Template<T>() where T : class, IAiOutput<T>
    {
        try
        {
            var value = JsonSerializer.Deserialize<T>(_templates[AiOutputs.FunctionOf<T>()], AiText.Strict);
            return value is not null && value.IsValid() ? value : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void Validate()
    {
        if (Template<AsistenteKnockSalida>() is null || Template<ResumenChatSalida>() is null || Template<BorradorCotizacionSalida>() is null)
        {
            throw new InvalidOperationException("An assistant fallback template does not match its output schema.");
        }
    }

    private static string ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Assistant resource '{name}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string ExtractJson(string markdown)
    {
        var start = markdown.IndexOf(_fence + "json", StringComparison.Ordinal);
        var end = start < 0 ? -1 : markdown.IndexOf(_fence, start + 7, StringComparison.Ordinal);
        return start < 0 || end < 0 ? throw new InvalidOperationException("Assistant template has no json block.") : markdown[(start + 7)..end].Trim();
    }
}
