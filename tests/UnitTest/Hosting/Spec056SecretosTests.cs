using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace Knock.UnitTest.Hosting;

/// <summary>
/// Spec 056, R-05, R-07 y R-08 (T-06): ninguna clave de servicio vive en el repositorio y el gateway no conoce las claves internas. La prueba lee los archivos de configuración y los fuentes del repositorio; no
/// necesita contenedores.
/// </summary>
public sealed class Spec056SecretosTests
{
    private static readonly string[] _projects = ["Knock.Gateway", "Knock.Identity.Api", "Knock.Messaging.Api", "Knock.Market.Api"];

    [Fact]
    public void Configuration_FilesCarryNoServiceKeyValue_CA_056_08()
    {
        var root = RepositoryRoot();
        var files = _projects.SelectMany(project => Directory.EnumerateFiles(Path.Combine(root, "src", project), "appsettings*.json")).ToList();

        files.Should().NotBeEmpty();
        foreach (var file in files)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var (path, value) in Flatten(document.RootElement, string.Empty))
            {
                if (path.Contains("InternalTokens", StringComparison.OrdinalIgnoreCase) || path.EndsWith(":SigningKey", StringComparison.OrdinalIgnoreCase))
                {
                    value.Should().BeNullOrWhiteSpace($"{Path.GetFileName(file)} no debe traer el valor de {path}: las claves llegan por variables de entorno o Key Vault (spec 056, R-08)");
                }
            }
        }
    }

    [Fact]
    public void Gateway_DoesNotKnowTheInternalServiceKeys_CA_056_08()
    {
        var gateway = Path.Combine(RepositoryRoot(), "src", "Knock.Gateway");
        var separator = Path.DirectorySeparatorChar;
        var files = Directory.EnumerateFiles(gateway, "*.*", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                && !file.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
                && (file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".json", StringComparison.Ordinal)));

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            text.Should().NotContain("InternalTokens", $"{Path.GetFileName(file)}: el gateway no lee las claves de servicio (spec 056, R-05)");
            text.Should().NotContain("interno-key-", $"{Path.GetFileName(file)}: el gateway no lee las claves de servicio");
            text.Should().NotContain("avisos-key-", $"{Path.GetFileName(file)}: el gateway no lee las claves de servicio");
        }
    }

    [Fact]
    public void Gateway_HasNoRouteToInternalPaths_CA_056_08()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Knock.Gateway", "appsettings.json"));

        text.Should().NotContain("/interno", "ninguna ruta interna está en la lista cerrada del gateway (spec 056, R-07)");
    }

    private static List<(string Path, string? Value)> Flatten(JsonElement element, string prefix)
    {
        var items = new List<(string Path, string? Value)>();
        Collect(element, prefix, items);
        return items;
    }

    private static void Collect(JsonElement element, string prefix, List<(string Path, string? Value)> items)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                Collect(property.Value, prefix.Length == 0 ? property.Name : $"{prefix}:{property.Name}", items);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var child in element.EnumerateArray())
            {
                Collect(child, $"{prefix}:{index++}", items);
            }
        }
        else
        {
            items.Add((prefix, element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()));
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Knock.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Knock.slnx not found");
    }
}
