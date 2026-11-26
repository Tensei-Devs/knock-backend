using System.Text.Json;

namespace Knock.ContractTest;

/// <summary>TT-CI-02 (KNOCK-137): los contratos publicados en <c>docs/</c> son OpenAPI 3.x válidos, todas sus operaciones declaran respuestas y los errores con cuerpo usan ProblemDetails.</summary>
public sealed class OpenApiContractTests
{
    private static readonly string[] _searchParameters = ["coloniaId", "cursor", "limit", "oficio", "q", "soloVerificados"];
    [Fact]
    public void Search_Declares_All_Query_Parameters_CA_027_12()
    {
        using var document = Load("openapi.v1.json");
        var operation = document.RootElement.GetProperty("paths").GetProperty("/api/v{version}/busqueda").GetProperty("get");
        var parameters = operation.GetProperty("parameters").EnumerateArray()
            .Where(parameter => parameter.GetProperty("in").GetString() == "query").ToArray();
        Assert.Equal(_searchParameters, parameters.Select(parameter => parameter.GetProperty("name").GetString()).Order(StringComparer.Ordinal));
        Assert.All(parameters, parameter =>
        {
            Assert.False(string.IsNullOrWhiteSpace(parameter.GetProperty("description").GetString()));
            Assert.Equal("string", parameter.GetProperty("schema").GetProperty("type").GetString());
        });
    }

    private static readonly HashSet<string> _httpMethods = new(["get", "put", "post", "delete", "options", "head", "patch", "trace"], StringComparer.Ordinal);

    private static JsonDocument Load(string fileName) => OpenApiDocs.Load(fileName);

    private static IEnumerable<(string Path, string Method, JsonElement Operation)> Operations(JsonElement root)
    {
        foreach (var path in root.GetProperty("paths").EnumerateObject())
        {
            foreach (var item in path.Value.EnumerateObject().Where(property => _httpMethods.Contains(property.Name)))
            {
                yield return (path.Name, item.Name, item.Value);
            }
        }
    }

    [Theory]
    [InlineData("openapi.v1.json")]
    [InlineData("openapi.identity.v1.json")]
    [InlineData("openapi.messaging.v1.json")]
    public void ContractFile_IsOpenApi3WithVersionAndPaths_TC_TT_CI_02_01(string fileName)
    {
        using var document = Load(fileName);
        var root = document.RootElement;

        Assert.StartsWith("3.", root.GetProperty("openapi").GetString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("info").GetProperty("version").GetString()));
        var paths = root.GetProperty("paths").EnumerateObject().ToList();
        Assert.NotEmpty(paths);
    }

    [Theory]
    [InlineData("openapi.v1.json")]
    [InlineData("openapi.identity.v1.json")]
    [InlineData("openapi.messaging.v1.json")]
    public void EveryOperation_DeclaresAtLeastOneResponse_TC_TT_CI_02_02(string fileName)
    {
        using var document = Load(fileName);

        var withoutResponses = Operations(document.RootElement)
            .Where(operation => !operation.Operation.TryGetProperty("responses", out var responses)
                || responses.ValueKind != JsonValueKind.Object
                || !responses.EnumerateObject().Any())
            .Select(operation => $"{operation.Method.ToUpperInvariant()} {operation.Path}")
            .ToList();

        Assert.Empty(withoutResponses);
    }

    [Theory]
    [InlineData("openapi.v1.json")]
    [InlineData("openapi.identity.v1.json")]
    [InlineData("openapi.messaging.v1.json")]
    public void ErrorResponsesWithABody_UseProblemDetails_TC_TT_CI_02_03(string fileName)
    {
        using var document = Load(fileName);

        var errorsWithoutProblemDetails = Operations(document.RootElement)
            .SelectMany(operation => operation.Operation.GetProperty("responses").EnumerateObject()
                .Where(response => response.Name.Length > 0
                    && response.Name[0] is '4' or '5'
                    && response.Value.TryGetProperty("content", out var content)
                    && !content.GetRawText().Contains("ProblemDetails", StringComparison.Ordinal))
                .Select(response => $"{operation.Method.ToUpperInvariant()} {operation.Path} {response.Name}"))
            .ToList();

        Assert.Empty(errorsWithoutProblemDetails);
    }

    [Fact]
    public void MessagingContract_ClosesConversationStateReasonAndDeviceErrors_TC_030_CONTRATO_01()
    {
        using var document = Load("openapi.messaging.v1.json");
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var view = schemas.GetProperty("ConversacionView");

        static string[] Enum(JsonElement property) => [.. property.GetProperty("enum").EnumerateArray().Select(item => item.GetString()!)];
        Assert.Equal(["abierta", "cerrada"], Enum(view.GetProperty("properties").GetProperty("estado")));
        Assert.Equal(
            ["request_cancelled", "request_expired", "other_quote_chosen", "dismissed", "service_finished"],
            Enum(view.GetProperty("properties").GetProperty("motivoCierre")));
        Assert.Equal("string", view.GetProperty("properties").GetProperty("avisoSeguridad").GetProperty("type").GetString());
        var required = view.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToList();
        Assert.Contains("avisoSeguridad", required);
        Assert.DoesNotContain("motivoCierre", required);

        var devices = document.RootElement.GetProperty("paths").GetProperty("/api/v{version}/dispositivos").GetProperty("post").GetProperty("responses");
        foreach (var code in new[] { "200", "201", "400", "401", "409", "422", "429", "503" })
        {
            Assert.True(devices.TryGetProperty(code, out _), $"POST /dispositivos should declare {code}.");
        }
    }
}
