using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Requests;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Tecnicos;

/// <summary>Spec 046, TEC-07 y TEC-13 por HTTP sobre MongoDB real: fotos de trabajos (CA-25, CA-26, CA-60) y reputación propia (CA-48, CA-49). Solo datos ficticios.</summary>
public sealed class Spec046FotosReputacionHttpTests
{
    private static async Task<(ObjectId Id, string Sub)> SeedTechnicianAsync(Spec020Host host)
    {
        var id = ObjectId.GenerateNewId();
        var sub = Guid.NewGuid().ToString("D");
        await host.Database.GetCollection<BsonDocument>("tecnicos").InsertOneAsync(new BsonDocument
        {
            ["_id"] = id,
            ["usuarioId"] = sub,
            ["nombre"] = "Ana Pruebas",
            ["estado"] = "activo",
            ["oculto"] = false,
            ["oficios"] = new BsonArray { Spec020Host.Plumbing },
            ["reputacion"] = new BsonDocument
            {
                ["promedio"] = 4.5,
                ["total"] = 4,
                ["distribucion"] = new BsonDocument { ["1"] = 0, ["2"] = 0, ["3"] = 0, ["4"] = 2, ["5"] = 2 },
                ["nivel"] = "confiable",
            },
        });
        return (id, sub);
    }

    private static BsonDocument Photo(ObjectId technician, string url, bool permission, bool moderated) => new()
    {
        ["tecnicoId"] = technician,
        ["visitaId"] = ObjectId.GenerateNewId(),
        ["url"] = url,
        ["permiso"] = permission,
        ["moderada"] = moderated,
        ["creadoEn"] = DateTime.UtcNow,
    };

    [Fact]
    public async Task Con_Sesion_Responde_200_Con_Las_Fotos_CA_046_25()
    {
        await using var host = await Spec020Host.StartAsync();
        var (id, _) = await SeedTechnicianAsync(host);
        await host.Database.GetCollection<BsonDocument>("fotos_trabajos").InsertOneAsync(Photo(id, "https://fotos.example.test/a.jpg", true, true));

        using var response = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{id}/fotos-trabajos", host.ClientSub);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await Spec020Host.ReadAsync(response);
        json.GetProperty("fotos").GetArrayLength().Should().Be(1);
        json.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Sin_Token_Responde_401_En_Fotos_CA_046_26()
    {
        await using var host = await Spec020Host.StartAsync();
        var (id, _) = await SeedTechnicianAsync(host);

        using var response = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{id}/fotos-trabajos");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Solo_Salen_Las_Fotos_Con_Permiso_Y_Moderadas_CA_046_60()
    {
        await using var host = await Spec020Host.StartAsync();
        var (id, _) = await SeedTechnicianAsync(host);
        await host.Database.GetCollection<BsonDocument>("fotos_trabajos").InsertManyAsync(
        [
            Photo(id, "https://fotos.example.test/ok.jpg", true, true),
            Photo(id, "https://fotos.example.test/sin-permiso.jpg", false, true),
            Photo(id, "https://fotos.example.test/sin-moderar.jpg", true, false),
        ]);

        using var response = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{id}/fotos-trabajos", host.ClientSub);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var photos = (await Spec020Host.ReadAsync(response)).GetProperty("fotos").EnumerateArray().Select(p => p.GetProperty("url").GetString()).ToList();
        photos.Should().Equal("https://fotos.example.test/ok.jpg");
    }

    [Fact]
    public async Task Tecnico_Con_Sesion_Recibe_Su_Reputacion_CA_046_48()
    {
        await using var host = await Spec020Host.StartAsync();
        var (_, sub) = await SeedTechnicianAsync(host);

        using var response = await host.SendAsync(HttpMethod.Get, "/api/v1/tecnicos/me/reputacion", sub);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await Spec020Host.ReadAsync(response);
        json.GetProperty("promedio").GetDouble().Should().Be(4.5);
        json.GetProperty("total").GetInt32().Should().Be(4);
        json.GetProperty("nivel").GetString().Should().Be("confiable");
        json.GetProperty("distribucion").GetProperty("5").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Sin_Token_Responde_401_En_Reputacion_CA_046_49()
    {
        await using var host = await Spec020Host.StartAsync();

        using var response = await host.SendAsync(HttpMethod.Get, "/api/v1/tecnicos/me/reputacion");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
