extern alias IdentityApi;
extern alias MarketApi;
extern alias MessagingApi;

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Knock.ContractTest;
using Knock.ContractTest.Support;
using Knock.IntegrationTest.Chat;
using Knock.IntegrationTest.Support;

namespace Knock.IntegrationTest.Contract;

/// <summary>
/// Spec 034 (R-16, CA-13): cada servicio arranca con sus contenedores (documentdb-local y Redis reales) y, por cada operación de su contrato publicado, se envía UNA petición
/// sin credenciales y sin datos válidos (cuerpo <c>{}</c> en POST/PUT/PATCH; ids de ruta sintéticos). La respuesta debe cumplir el contrato: su código está entre los documentados de la
/// operación, el tipo de contenido es uno de los declarados, el cuerpo cumple el esquema declarado y los encabezados que el contrato declara para ese código están presentes.
/// Criterio (sin falsos positivos): solo se provocan los códigos que se obtienen sin credenciales ni estado previo (401 en las operaciones protegidas; 400, 422 o el éxito en las
/// anónimas, según lo que valide cada una). Los códigos que necesitan una persona con rol, un recurso existente o un servicio externo (403, 404, 409, 413, 429 y 503) no se provocan
/// aquí: los cubren las pruebas de cada spec y la comparación de rutas de <c>Spec034RutasTests</c>. Solo datos ficticios.
/// </summary>
public sealed partial class Spec034ContratoVivoTests
{
    private const string _syntheticId = "507f1f77bcf86cd799439011";
    private static readonly string[] _bodyMethods = ["POST", "PUT", "PATCH"];

    [Fact]
    public async Task Identidad_Responde_Segun_Su_Contrato_CA_034_13()
    {
        await using var identity = await IdentityApiFactory.CreateAsync();
        using var client = identity.CreateClient();

        var failures = await CheckAsync("openapi.identity.v1.json", client);

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task Mercado_Responde_Segun_Su_Contrato_CA_034_13()
    {
        await using var market = await MarketApiFactory.CreateAsync();
        using var client = market.CreateClient();

        var failures = await CheckAsync("openapi.v1.json", client);

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task Mensajeria_Responde_Segun_Su_Contrato_CA_034_13()
    {
        await using var fakeMarket = await FakeMarket.StartAsync();
        await using var messaging = await ChatFactory.CreateAsync(fakeMarket);
        using var client = messaging.CreateClient();

        var failures = await CheckAsync("openapi.messaging.v1.json", client);

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static async Task<List<string>> CheckAsync(string fileName, HttpClient client)
    {
        using var contract = OpenApiDocs.Load(fileName);
        var validator = new SchemaValidator(contract.RootElement);
        var failures = new List<string>();
        foreach (var item in ContractReader.ContractOperations(contract.RootElement).Where(item => !item.Contains(" /health", StringComparison.Ordinal)))
        {
            var (method, path) = Split(item);
            var operation = contract.RootElement.GetProperty("paths").GetProperty(path).EnumerateObject()
                .First(property => string.Equals(property.Name, method, StringComparison.OrdinalIgnoreCase)).Value;
            using var request = new HttpRequestMessage(new HttpMethod(method), Url(path));
            if (_bodyMethods.Contains(method, StringComparer.Ordinal))
            {
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            }

            using var response = await client.SendAsync(request);
            var raw = await response.Content.ReadAsStringAsync();
            failures.AddRange(Compare(fileName, item, operation, response, raw, validator));
        }

        return failures;
    }

    private static IEnumerable<string> Compare(string fileName, string item, JsonElement operation, HttpResponseMessage response, string raw, SchemaValidator validator)
    {
        var code = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        var prefix = $"{fileName}: {item} -> {code}";
        var responses = operation.GetProperty("responses");
        if (!responses.TryGetProperty(code, out var declared))
        {
            yield return $"{prefix}: el contrato no documenta ese código (documenta {string.Join(", ", responses.EnumerateObject().Select(property => property.Name))})";
            yield break;
        }

        foreach (var problem in HeaderProblems(declared, response))
        {
            yield return $"{prefix}: {problem}";
        }

        if (raw.Length == 0 || !declared.TryGetProperty("content", out var content))
        {
            yield break;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (!content.TryGetProperty(mediaType, out var media))
        {
            yield return $"{prefix}: tipo de contenido {mediaType} no declarado";
            yield break;
        }

        foreach (var problem in BodyProblems(media, raw, validator))
        {
            yield return $"{prefix}: {problem}";
        }
    }

    private static IEnumerable<string> HeaderProblems(JsonElement declared, HttpResponseMessage response)
    {
        if (!declared.TryGetProperty("headers", out var headers))
        {
            yield break;
        }

        foreach (var header in headers.EnumerateObject().Where(header => !response.Headers.Contains(header.Name) && !response.Content.Headers.Contains(header.Name)))
        {
            yield return $"falta el encabezado declarado {header.Name}";
        }
    }

    private static List<string> BodyProblems(JsonElement media, string raw, SchemaValidator validator)
    {
        if (!media.TryGetProperty("schema", out var schema))
        {
            return [];
        }

        try
        {
            using var body = JsonDocument.Parse(raw);
            return [.. validator.Validate(schema, body.RootElement).Select(problem => "cuerpo: " + problem)];
        }
        catch (JsonException)
        {
            return ["el cuerpo no es JSON"];
        }
    }

    private static (string Method, string Path) Split(string item)
    {
        var index = item.IndexOf(' ', StringComparison.Ordinal);
        return (item[..index], item[(index + 1)..]);
    }

    private static Uri Url(string path) =>
        new(ParameterRegex().Replace(path.Replace("{version}", "1", StringComparison.Ordinal), _syntheticId), UriKind.Relative);

    [GeneratedRegex(@"\{[A-Za-z0-9_]+\}")]
    private static partial Regex ParameterRegex();
}
