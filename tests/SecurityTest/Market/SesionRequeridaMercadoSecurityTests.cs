using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using MongoDB.Bson;

namespace Knock.SecurityTest.Market;

/// <summary>Specs 047 y 032: crear y editar un servicio del técnico y confirmar una subida de imagen exigen sesión. Mercado real en Production; solo datos ficticios.</summary>
public sealed class SesionRequeridaMercadoSecurityTests
{
    private const string _services = "/api/v1/tecnicos/me/servicios";

    [Fact]
    public async Task CreatingAndEditingAService_RequireASession_CA_047_04()
    {
        var name = "market_ses_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        using var factory = await SecurityMarketFactory.CreateAsync(new Dictionary<string, string?> { ["Mongo:Databases:Catalog"] = name, ["Mongo:Databases:Tecnicos"] = name });
        using var client = factory.CreateClient();
        await factory.AddOficioAsync("plo", "Plomería", "domicilio", activo: true);
        var technician = TokenForge.Sub();
        await factory.Database.GetCollection<BsonDocument>("tecnicos").InsertOneAsync(new BsonDocument
        {
            ["usuarioId"] = technician,
            ["nombre"] = "Técnico Demo",
            ["estado"] = "activo",
            ["oculto"] = false,
            ["oficios"] = new BsonArray { "plo" },
        });
        var owner = TokenForge.Valid(technician);
        var body = new { nombre = "Cambio de llave", detalle = "Incluye materiales", precioReferencia = 350, oficio = "plo" };

        foreach (var token in new[] { null, TokenForge.Expired(technician) })
        {
            using var create = await SecureHttp.SendRawJsonAsync(client, HttpMethod.Post, _services, JsonSerializer.Serialize(body), token);

            await SecureHttp.AssertProblemAsync(create, HttpStatusCode.Unauthorized);
        }

        // Control: con sesión sí se crea, y es el único (lo rechazado no dejó nada).
        using var created = await SecureHttp.PostJsonAsync(client, _services, body, owner);
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var id = (await SecureHttp.ReadJsonAsync(created)).GetProperty("id").GetString();
        var path = $"{_services}/{id}";

        foreach (var token in new[] { null, TokenForge.Expired(technician) })
        {
            using var edit = await SecureHttp.SendRawJsonAsync(client, HttpMethod.Put, path, """{"precioReferencia":999}""", token);

            await SecureHttp.AssertProblemAsync(edit, HttpStatusCode.Unauthorized);
        }

        using var edited = await SecureHttp.SendRawJsonAsync(client, HttpMethod.Put, path, """{"precioReferencia":400}""", owner);

        edited.StatusCode.Should().Be(HttpStatusCode.OK, await edited.Content.ReadAsStringAsync());
        (await SecureHttp.ReadJsonAsync(edited)).GetProperty("precioReferencia").GetInt32().Should().Be(400);
    }

    [Fact]
    public async Task ConfirmingAnImageUpload_RequiresASession_CA_032_02()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var path = $"/api/v1/imagenes/subidas/{Guid.NewGuid():N}/confirmar";

        foreach (var token in new[] { null, TokenForge.Expired() })
        {
            using var response = await SecureHttp.SendAsync(hosts.MarketClient, HttpMethod.Post, path, bearer: token);

            await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
        }
    }
}
