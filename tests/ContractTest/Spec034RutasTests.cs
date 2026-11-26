using System.Text.Json;
using Knock.ContractTest.Support;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Knock.ContractTest;

/// <summary>Spec 034 (R-16, CA-13): el contrato de cada servicio y las rutas reales del servicio coinciden en ambos sentidos, y el validador de esquemas detecta cuerpos que no coinciden.</summary>
public sealed class Spec034RutasTests
{
    [Fact]
    public void Rutas_Y_Esquemas_Coinciden_CA_034_13()
    {
        using var identity = ServiceContractHosts.Identity();
        using var market = ServiceContractHosts.Market();
        using var messaging = ServiceContractHosts.Messaging();
        var failures = new List<string>();
        failures.AddRange(Compare("openapi.identity.v1.json", Sources(identity.Services)));
        failures.AddRange(Compare("openapi.v1.json", Sources(market.Services)));
        failures.AddRange(Compare("openapi.messaging.v1.json", Sources(messaging.Services)));

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static readonly string[] _serviceContracts = ["openapi.v1.json", "openapi.identity.v1.json", "openapi.messaging.v1.json"];
    private static readonly string[] _internalHosts = ["knock-identity", "knock-market", "knock-messaging", "knock-redis", ".internal."];

    [Fact]
    public void Publico_Es_Union_De_Los_Contratos_De_Servicio_CA_034_11()
    {
        using var publico = OpenApiDocs.Load("openapi.publico.v1.json");
        var services = _serviceContracts.Select(OpenApiDocs.Load).ToList();
        try
        {
            var all = services.SelectMany(doc => ContractReader.ContractOperations(doc.RootElement)).ToHashSet(StringComparer.Ordinal);
            var stale = ContractReader.ContractOperations(publico.RootElement).Where(item => !all.Contains(item)).ToList();

            Assert.True(stale.Count == 0, "openapi.publico.v1.json: operaciones que ya no están en ningún servicio: " + string.Join(", ", stale));
        }
        finally
        {
            services.ForEach(doc => doc.Dispose());
        }
    }

    [Fact]
    public void Publico_Tiene_Rutas_De_Identidad_Y_Mercado_Y_Ninguna_Interna_CA_034_14()
    {
        using var publico = OpenApiDocs.Load("openapi.publico.v1.json");
        var operations = ContractReader.ContractOperations(publico.RootElement);

        Assert.Contains("POST /api/v{version}/auth/refresh", operations);
        Assert.Contains("GET /api/v{version}/busqueda", operations);
        Assert.DoesNotContain(operations, item => item.Contains("/interno", StringComparison.Ordinal) || item.Contains(" /health", StringComparison.Ordinal));
    }

    [Fact]
    public void Publico_No_Revela_Servers_Ni_Hosts_Internos_CA_034_18()
    {
        using var publico = OpenApiDocs.Load("openapi.publico.v1.json");
        var text = publico.RootElement.GetRawText();

        Assert.False(publico.RootElement.TryGetProperty("servers", out _), "El contrato público no declara servers.");
        Assert.DoesNotContain(_internalHosts, host => text.Contains(host, StringComparison.Ordinal));
        Assert.DoesNotContain("+52", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("openapi.v1.json")]
    [InlineData("openapi.identity.v1.json")]
    [InlineData("openapi.messaging.v1.json")]
    public void Referencias_Del_Contrato_Se_Resuelven_CA_034_13(string fileName)
    {
        using var document = OpenApiDocs.Load(fileName);

        Assert.Empty(new SchemaValidator(document.RootElement).UnresolvedReferences().Distinct(StringComparer.Ordinal));
    }

    [Fact]
    public void Validador_Acepta_Cuerpo_Valido_Y_Nombra_Campo_Faltante_O_Tipo_Erroneo_CA_034_13()
    {
        using var contract = JsonDocument.Parse("""
            {"components":{"schemas":{"Item":{"type":"object","required":["id","name"],"properties":{"id":{"type":"integer"},"name":{"type":["string","null"]},"tags":{"type":"array","items":{"type":"string"}}}}}}}
            """);
        var validator = new SchemaValidator(contract.RootElement);
        using var schema = JsonDocument.Parse("""{"$ref":"#/components/schemas/Item"}""");
        using var valid = JsonDocument.Parse("""{"id":1,"name":null,"tags":["a"]}""");
        using var missing = JsonDocument.Parse("""{"id":1}""");
        using var wrong = JsonDocument.Parse("""{"id":"x","name":"n","tags":[1]}""");

        Assert.Empty(validator.Validate(schema.RootElement, valid.RootElement));
        Assert.Contains(validator.Validate(schema.RootElement, missing.RootElement), problem => problem.Contains("name", StringComparison.Ordinal));
        var problems = validator.Validate(schema.RootElement, wrong.RootElement);
        Assert.Contains(problems, problem => problem.StartsWith("$.id", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.StartsWith("$.tags[0]", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("api/v{version:apiVersion}/x/{id:guid}", "/api/v{version}/x/{id}")]
    [InlineData("/api/v{version:apiVersion}/y/{id?}", "/api/v{version}/y/{id}")]
    [InlineData("api/v{version:apiVersion}/z", "/api/v{version}/z")]
    public void Normaliza_Rutas_Como_El_Contrato_CA_034_13(string pattern, string expected) =>
        Assert.Equal(expected, ContractReader.Normalize(pattern));

    private static IEnumerable<EndpointDataSource> Sources(IServiceProvider services) => services.GetServices<EndpointDataSource>();

    private static IEnumerable<string> Compare(string fileName, IEnumerable<EndpointDataSource> sources)
    {
        using var document = OpenApiDocs.Load(fileName);
        // /health y /health/ready son endpoints mínimos (no controllers) que algunos contratos declaran: quedan fuera de la comparación.
        var contract = ContractReader.ContractOperations(document.RootElement).Where(item => !item.Contains(" /health", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        var host = ContractReader.HostOperations(sources);
        return host.Except(contract).Select(item => $"{fileName}: el servicio atiende {item} y el contrato no lo declara")
            .Concat(contract.Except(host).Select(item => $"{fileName}: el contrato declara {item} y el servicio no lo atiende"));
    }
}
