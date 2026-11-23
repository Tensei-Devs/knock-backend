using System.Text.Json;

namespace Knock.ContractTest;

/// <summary>Spec 034 (R-14, CA-15): cada contrato de servicio declara los campos y encabezados de error comunes.</summary>
public sealed class Spec034ContratoTests
{
    private static readonly HashSet<string> _httpMethods = new(["get", "put", "post", "delete", "patch"], StringComparer.Ordinal);

    [Theory]
    [InlineData("openapi.v1.json")]
    [InlineData("openapi.identity.v1.json")]
    [InlineData("openapi.messaging.v1.json")]
    public void Errores_Y_Encabezados_Declarados_CA_034_15(string fileName)
    {
        using var document = Load(fileName);
        var root = document.RootElement;
        var problem = root.GetProperty("components").GetProperty("schemas").GetProperty("ProblemDetails").GetProperty("properties");
        foreach (var field in new[] { "code", "correlationId", "traceId" })
        {
            Assert.True(problem.TryGetProperty(field, out _), $"{fileName}: ProblemDetails debe declarar {field}.");
        }

        Assert.Empty(Responses(root).SelectMany(item => Missing(item.Label, item.Code, item.Response)));
    }

    private static IEnumerable<string> Missing(string label, string code, JsonElement response)
    {
        var headers = response.TryGetProperty("headers", out var found) ? found : default;
        if (!HasHeader(headers, "X-Correlation-Id"))
        {
            yield return label + " sin X-Correlation-Id";
        }

        if (code is "429" or "503" && !HasHeader(headers, "Retry-After"))
        {
            yield return label + " sin Retry-After";
        }
    }

    private static bool HasHeader(JsonElement headers, string name) =>
        headers.ValueKind == JsonValueKind.Object && headers.TryGetProperty(name, out _);

    private static IEnumerable<(string Label, string Code, JsonElement Response)> Responses(JsonElement root) =>
        from path in root.GetProperty("paths").EnumerateObject()
        from operation in path.Value.EnumerateObject()
        where _httpMethods.Contains(operation.Name)
        from response in operation.Value.GetProperty("responses").EnumerateObject()
        select ($"{operation.Name.ToUpperInvariant()} {path.Name} {response.Name}", response.Name, response.Value);

    private static JsonDocument Load(string fileName)
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
