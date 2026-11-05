extern alias IdentityApi;

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Xunit;

namespace Knock.IntegrationTest.Identity;

/// <summary>
/// Spec 002 sobre Identity: ruta inexistente en ProblemDetails (CA-01), <c>/health/ready</c> con MongoDB caído (CA-03) y contrato OpenAPI
/// publicado al día (CA-04). Servicios reales en contenedor; solo datos ficticios.
/// </summary>
public class Spec002ApiStyleTests
{
    [Fact]
    public async Task UnknownRoute_Returns404AsProblemDetails_CA_002_01()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/esta-ruta-no-existe", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(404);
        problem.TryGetProperty("title", out _).Should().BeTrue();
        problem.TryGetProperty("type", out _).Should().BeTrue();
        (problem.TryGetProperty("traceId", out _) || problem.TryGetProperty("correlationId", out _)).Should().BeTrue("el cliente necesita un identificador para reportar el error");
    }

    [Fact]
    public async Task Ready_WithMongoDown_Returns503_AndWithItUpReturns200_CA_002_03()
    {
        var redis = await RedisTestFixture.SharedAsync();
        var mongo = await DocumentDbFixture.SharedAsync();
        using var healthy = new IdentityApiFactory(Settings(mongo.ConnectionString, redis.ConnectionString));
        using var broken = new IdentityApiFactory(Settings("mongodb://127.0.0.1:9/?serverSelectionTimeoutMS=500&connectTimeoutMS=500", redis.ConnectionString));

        using var up = await healthy.CreateClient().GetAsync(new Uri("/health/ready", UriKind.Relative));
        using var down = await broken.CreateClient().GetAsync(new Uri("/health/ready", UriKind.Relative));

        up.StatusCode.Should().Be(HttpStatusCode.OK, "con MongoDB y Redis vivos el servicio está listo");
        down.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, "sin MongoDB el servicio no está listo");
    }

    [Fact]
    public async Task PublishedContract_ListsTheSameOperationsAsTheRunningService_CA_002_04()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var live = JsonDocument.Parse(await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative)));
        var published = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "docs", "openapi.identity.v1.json")));

        Operations(published).Should().BeEquivalentTo(Operations(live), "un endpoint nuevo o cambiado obliga a regenerar docs/openapi.identity.v1.json (python scripts/export_openapi.py --service identity)");
    }

    private static Dictionary<string, string?> Settings(string mongo, string redis) => new()
    {
        ["ConnectionStrings:KnockMongo"] = mongo,
        ["ConnectionStrings:KnockRedis"] = redis,
        ["Mongo:Databases:Identity"] = "identity_s002_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6)),
    };

    private static List<string> Operations(JsonDocument contract) =>
        [.. contract.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(operation => $"{operation.Name.ToUpperInvariant()} {path.Name}"))
            .Order(StringComparer.Ordinal)];

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Knock.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No se encontró Knock.slnx subiendo desde el directorio de pruebas.");
    }
}
