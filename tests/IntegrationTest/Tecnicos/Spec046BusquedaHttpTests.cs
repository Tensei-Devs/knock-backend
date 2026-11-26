using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Requests;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Tecnicos;

/// <summary><c>GET /tecnicos/{id}/llega-a-mi-colonia</c> (TEC-06) y <c>GET /tecnicos/autocompletar</c> (TEC-03) sobre MongoDB real. Solo datos ficticios.</summary>
public sealed class Spec046BusquedaHttpTests
{
    private static async Task<ObjectId> SeedAsync(Spec020Host host, bool oculto = false, string negocio = "Plomería Ana")
    {
        var id = ObjectId.GenerateNewId();
        await host.Database.GetCollection<BsonDocument>("tecnicos").InsertOneAsync(new BsonDocument
        {
            ["_id"] = id,
            ["usuarioId"] = Guid.NewGuid().ToString("D"),
            ["nombre"] = "Ana Pruebas",
            ["nombreNegocio"] = negocio,
            ["estado"] = "activo",
            ["oculto"] = oculto,
            ["oficios"] = new BsonArray { Spec020Host.Plumbing },
            ["atencion"] = "taller",
            ["coloniaBaseId"] = host.ColoniaId.ToString(),
        });
        return id;
    }

    [Fact]
    public async Task Llega_A_Su_Colonia_Base_Responde_200_CA_046_20()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await SeedAsync(host);
        using var response = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{id}/llega-a-mi-colonia?colonia={host.ColoniaId}", host.ClientSub);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Spec020Host.ReadAsync(response)).GetProperty("atiende").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Llega_A_Mi_Colonia_Sin_Token_Responde_401_CA_046_21()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await SeedAsync(host);
        using var response = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{id}/llega-a-mi-colonia?colonia={host.ColoniaId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Colonia_Mal_Escrita_O_Fuera_Del_Catalogo_Responde_422_CA_046_22()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await SeedAsync(host);
        using var malformed = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{id}/llega-a-mi-colonia?colonia=centro", host.ClientSub);
        using var unknown = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{id}/llega-a-mi-colonia?colonia={ObjectId.GenerateNewId()}", host.ClientSub);
        malformed.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        ProblemBody.Reasons(await malformed.Content.ReadAsStringAsync()).Should().NotContain("centro");
        unknown.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Spec020Host.ReadAsync(unknown)).GetProperty("code").GetString().Should().Be("colonia_not_found");
    }

    [Fact]
    public async Task Tecnico_Oculto_Responde_404_CA_046_23()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await SeedAsync(host, oculto: true);
        using var response = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{id}/llega-a-mi-colonia?colonia={host.ColoniaId}", host.ClientSub);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("technician_not_found");
    }

    [Fact]
    public async Task Autocompletar_Sin_Sesion_Sugiere_Oficios_Y_Negocios_Visibles_CA_046_10()
    {
        await using var host = await Spec020Host.StartAsync();
        await SeedAsync(host, negocio: "Plomería Ana");
        await SeedAsync(host, oculto: true, negocio: "Plomería Oculta");

        using var response = await host.SendAsync(HttpMethod.Get, "/api/v1/tecnicos/autocompletar?q=plomer");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var texts = (await Spec020Host.ReadAsync(response)).GetProperty("sugerencias").EnumerateArray().Select(s => s.GetProperty("texto").GetString()).ToList();
        texts.Should().Contain("Plomería").And.Contain("Plomería Ana").And.NotContain("Plomería Oculta");
        texts.Should().HaveCountLessThanOrEqualTo(8);
    }

    [Fact]
    public async Task Autocompletar_Sin_Acentos_Sugiere_Servicios_Y_Negocios_Solo_De_Visibles_CA_046_10()
    {
        await using var host = await Spec020Host.StartAsync();
        var visible = await SeedAsync(host, negocio: "Fontanería Óscar");
        var hidden = await SeedAsync(host, oculto: true, negocio: "Fontanería Oculta");
        await host.Database.GetCollection<BsonDocument>("servicios_tecnico").InsertManyAsync(
        [
            new BsonDocument { ["tecnicoId"] = visible, ["nombre"] = "Reparación de calentador" },
            new BsonDocument { ["tecnicoId"] = hidden, ["nombre"] = "Reparación secreta" },
        ]);

        using var services = await host.SendAsync(HttpMethod.Get, "/api/v1/tecnicos/autocompletar?q=reparacion");
        using var businesses = await host.SendAsync(HttpMethod.Get, "/api/v1/tecnicos/autocompletar?q=fontaneria%20oscar");

        var found = (await Spec020Host.ReadAsync(services)).GetProperty("sugerencias").EnumerateArray().ToList();
        found.Should().ContainSingle();
        found[0].GetProperty("tipo").GetString().Should().Be("servicio");
        found[0].GetProperty("texto").GetString().Should().Be("Reparación de calentador");
        found[0].GetProperty("id").GetString().Should().Be(visible.ToString());
        (await Spec020Host.ReadAsync(businesses)).GetProperty("sugerencias").EnumerateArray().Select(s => s.GetProperty("texto").GetString())
            .Should().ContainSingle().Which.Should().Be("Fontanería Óscar");
    }

    private static async Task<IReadOnlyList<JsonElement>> SuggestAsync(Spec020Host host, string query)
    {
        using var response = await host.SendAsync(HttpMethod.Get, "/api/v1/tecnicos/autocompletar?q=" + Uri.EscapeDataString(query));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return [.. (await Spec020Host.ReadAsync(response)).GetProperty("sugerencias").EnumerateArray()];
    }

    private static Task InsertServiceAsync(Spec020Host host, ObjectId technician, string name) =>
        host.Database.GetCollection<BsonDocument>("servicios_tecnico").InsertOneAsync(new BsonDocument { ["tecnicoId"] = technician, ["nombre"] = name });

    [Fact]
    public async Task Autocompletar_Encuentra_Un_Servicio_Creado_Por_La_Api_CA_046_10()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await SeedAsync(host);
        var sub = (await host.Database.GetCollection<BsonDocument>("tecnicos").Find(new BsonDocument("_id", id)).SingleAsync())["usuarioId"].AsString;
        using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/tecnicos/me/servicios", sub,
            new { nombre = "Instalación de piñón", precioReferencia = 350, oficio = Spec020Host.Plumbing });
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var found = await SuggestAsync(host, "pinon");

        found.Should().ContainSingle();
        found[0].GetProperty("texto").GetString().Should().Be("Instalación de piñón");
        found[0].GetProperty("id").GetString().Should().Be(id.ToString());
    }

    [Fact]
    public async Task Autocompletar_No_Sugiere_Servicios_De_Suspendidos_Ni_Ocultos_Por_Moderacion_CA_046_10()
    {
        await using var host = await Spec020Host.StartAsync();
        var suspended = await SeedAsync(host, negocio: "Taller Uno");
        var moderated = await SeedAsync(host, negocio: "Taller Dos");
        var technicians = host.Database.GetCollection<BsonDocument>("tecnicos");
        await technicians.UpdateOneAsync(new BsonDocument("_id", suspended), Builders<BsonDocument>.Update.Set("estado", "suspendido"));
        await technicians.UpdateOneAsync(new BsonDocument("_id", moderated), Builders<BsonDocument>.Update.Set("ocultoHasta", DateTime.UtcNow.AddDays(1)));
        await InsertServiceAsync(host, suspended, "Destape de drenaje");
        await InsertServiceAsync(host, moderated, "Destape de tubería");

        (await SuggestAsync(host, "destape")).Should().BeEmpty();
    }

    [Fact]
    public async Task Autocompletar_No_Repite_Servicios_Y_Respeta_El_Maximo_De_Ocho_CA_046_10()
    {
        await using var host = await Spec020Host.StartAsync();
        var first = await SeedAsync(host, negocio: "Cambio Express");
        var second = await SeedAsync(host, negocio: "Taller Dos");
        await InsertServiceAsync(host, first, "Cambio de válvula");
        await InsertServiceAsync(host, second, "Cambio de valvula");
        for (var index = 1; index <= 10; index++)
        {
            await InsertServiceAsync(host, first, $"Cambio de llave {index:00}");
        }

        var found = await SuggestAsync(host, "cambio de");

        found.Should().HaveCount(8);
        found.Select(s => TextWithoutAccent(s)).Should().OnlyHaveUniqueItems();
        found.Should().OnlyContain(s => s.GetProperty("tipo").GetString() == "servicio");
    }

    private static string TextWithoutAccent(JsonElement suggestion) =>
        suggestion.GetProperty("texto").GetString()!.Replace('á', 'a');

    [Fact]
    public async Task Autocompletar_Con_Texto_Invalido_Responde_422_CA_046_11()
    {
        await using var host = await Spec020Host.StartAsync();
        using var shortText = await host.SendAsync(HttpMethod.Get, "/api/v1/tecnicos/autocompletar?q=p");
        using var symbols = await host.SendAsync(HttpMethod.Get, "/api/v1/tecnicos/autocompletar?q=%2A.%2A");
        shortText.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        symbols.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        ProblemBody.Reasons(await symbols.Content.ReadAsStringAsync()).Should().NotContain("*.*");
    }
}
