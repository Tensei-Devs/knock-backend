using System.Text.Json;

namespace Knock.ContractTest;

/// <summary>Lectura compartida de los contratos OpenAPI publicados en <c>docs/</c>.</summary>
internal static class OpenApiDocs
{
    public static JsonDocument Load(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Knock.slnx")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ?? throw new InvalidOperationException("Knock.slnx was not found above the test output directory.");
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs", fileName)));
    }
}
