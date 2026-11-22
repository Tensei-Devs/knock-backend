using System.Text.Json;

namespace Knock.ContractTest;

/// <summary>Spec 032, T-08: el contrato de Mercado publica los dos endpoints de imágenes públicas con sus códigos de respuesta (§5).</summary>
public sealed class Spec032ContractTests
{
    private const string _subidas = "/api/v{version}/imagenes/subidas";

    private static JsonDocument Load()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Knock.slnx")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ?? throw new InvalidOperationException("Knock.slnx was not found above the test output directory.");
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs", "openapi.v1.json")));
    }

    private static string[] Statuses(JsonDocument document, string path) =>
        [.. document.RootElement.GetProperty("paths").GetProperty(path).GetProperty("post").GetProperty("responses").EnumerateObject().Select(r => r.Name).Order(StringComparer.Ordinal)];

    [Fact]
    public void Emite_Enlace_Declara_Sus_Respuestas_CA_032_01()
    {
        using var document = Load();

        Assert.Equal(["201", "400", "401", "422", "429", "503"], Statuses(document, _subidas));
    }

    [Fact]
    public void Confirmar_Declara_Sus_Respuestas_CA_032_14()
    {
        using var document = Load();

        Assert.Equal(["200", "400", "401", "404", "409", "422", "429", "503"], Statuses(document, _subidas + "/{id}/confirmar"));
    }
}
