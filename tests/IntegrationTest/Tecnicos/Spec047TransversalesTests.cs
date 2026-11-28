using System.Net;
using AwesomeAssertions;
using Knock.IntegrationTest.Requests;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Tecnicos;

/// <summary>Spec 047, T-07 (R-18 a R-21): la persona es la del <c>sub</c>, registros sin datos personales, 429 y 413, y sin <c>Idempotency-Key</c>.</summary>
public sealed class Spec047TransversalesTests
{
    private const string _mine = "/api/v1/tecnicos/me/servicios";
    private const string _marker = "Detalle-privado-del-tecnico-047";

    private static async Task<(ObjectId Id, string Sub)> SeedTechnicianAsync(Spec020Host host)
    {
        var sub = Guid.NewGuid().ToString("D");
        var id = ObjectId.GenerateNewId();
        await host.Database.GetCollection<BsonDocument>("tecnicos").InsertOneAsync(new BsonDocument
        {
            { "_id", id }, { "usuarioId", sub }, { "nombre", "Demo Técnico" }, { "estado", "activo" }, { "oculto", false }, { "oficios", new BsonArray { Spec020Host.Plumbing } },
        });
        return (id, sub);
    }

    private static Dictionary<string, object?> Body(string otherPerson, string detail = "Incluye materiales") => new()
    {
        ["nombre"] = "Cambio de llave",
        ["detalle"] = detail,
        ["precioReferencia"] = 350,
        ["oficio"] = Spec020Host.Plumbing,
        ["sub"] = otherPerson,
        ["tecnicoId"] = otherPerson,
        ["usuarioId"] = otherPerson,
    };

    [Fact]
    public async Task Identificador_Ajeno_En_El_Cuerpo_Se_Ignora_CA_047_27()
    {
        await using var host = await Spec020Host.StartAsync();
        var (id, owner) = await SeedTechnicianAsync(host);
        var (otherId, other) = await SeedTechnicianAsync(host);

        using var created = await host.SendAsync(HttpMethod.Post, _mine, owner, Body(other));

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var services = host.Database.GetCollection<BsonDocument>("servicios_tecnico");
        (await services.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("tecnicoId", id))).Should().Be(1, "el servicio es del técnico del token");
        (await services.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("tecnicoId", otherId))).Should().Be(0);
    }

    [Fact]
    public async Task Registros_Y_Errores_Sin_Datos_Personales_CA_047_28()
    {
        await using var host = await Spec020Host.StartAsync();
        var (_, sub) = await SeedTechnicianAsync(host);
        var errorBody = string.Empty;

        var output = await ConsoleCapture.RunAsync(async () =>
        {
            using var created = await host.SendAsync(HttpMethod.Post, _mine, sub, Body(Guid.NewGuid().ToString("D"), _marker));
            using var invalid = await host.SendAsync(HttpMethod.Post, _mine, sub, rawJson: "{\"nombre\":\"" + new string('x', 61) + "\",\"precioReferencia\":0,\"oficio\":\"" + Spec020Host.Plumbing + "\"}");
            errorBody = await invalid.Content.ReadAsStringAsync();
        });

        output.Should().NotContain(_marker, "los textos escritos por la persona no se registran")
            .And.NotContain(sub, "el técnico no se registra")
            .And.NotContain("Demo Técnico");
        errorBody.Should().NotContain(new string('x', 61), "el error nunca repite el valor enviado");
        errorBody.Should().Contain("correlationId").And.Contain("traceId");
    }

    [Fact]
    public async Task Supera_30_Por_Minuto_Y_Responde_429_Con_RetryAfter_CA_047_29()
    {
        await using var host = await Spec020Host.StartAsync();
        var (id, _) = await SeedTechnicianAsync(host);
        var reader = Guid.NewGuid().ToString("D");
        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? last = null;

        for (var i = 0; i < 31; i++)
        {
            last?.Dispose();
            last = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{id}/servicios", reader);
            statuses.Add(last.StatusCode);
        }

        using (last)
        {
            statuses.Take(30).Should().OnlyContain(s => s == HttpStatusCode.OK);
            statuses[30].Should().Be(HttpStatusCode.TooManyRequests);
            last!.Headers.RetryAfter.Should().NotBeNull();
        }
    }

    [Theory]
    [InlineData("POST", "", 2100)]
    [InlineData("PUT", "/64b000000000000000000001", 2100)]
    [InlineData("DELETE", "/64b000000000000000000001", 1100)]
    public async Task Cuerpo_Mayor_Al_Tope_Responde_413_CA_047_29(string method, string suffix, int size)
    {
        await using var host = await Spec020Host.StartAsync();
        var (_, sub) = await SeedTechnicianAsync(host);

        using var response = await host.SendAsync(new HttpMethod(method), _mine + suffix, sub, rawJson: "{\"detalle\":\"" + new string('a', size) + "\"}");

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("payload_too_large");
    }

    [Fact]
    public async Task Crear_No_Pide_IdempotencyKey_Y_El_Tope_Impide_Duplicados_CA_047_30()
    {
        await using var host = await Spec020Host.StartAsync();
        var (id, sub) = await SeedTechnicianAsync(host);
        await host.Database.GetCollection<BsonDocument>("servicios_tecnico").InsertManyAsync(Enumerable.Range(0, 49).Select(i => new BsonDocument
        {
            { "tecnicoId", id }, { "nombre", $"Servicio {i}" }, { "precioReferencia", 350 }, { "oficio", Spec020Host.Plumbing },
        }));
        var body = Body(Guid.NewGuid().ToString("D"));

        using var first = await host.SendAsync(HttpMethod.Post, _mine, sub, body);
        using var repeated = await host.SendAsync(HttpMethod.Post, _mine, sub, body);

        first.StatusCode.Should().Be(HttpStatusCode.Created, "no exige Idempotency-Key");
        repeated.StatusCode.Should().Be(HttpStatusCode.Conflict, "el tope de 50 impide acumular repeticiones");
        (await host.Database.GetCollection<BsonDocument>("servicios_tecnico").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("tecnicoId", id))).Should().Be(50);
    }
}
