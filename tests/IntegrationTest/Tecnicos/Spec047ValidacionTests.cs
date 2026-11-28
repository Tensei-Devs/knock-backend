using System.Net;
using AwesomeAssertions;
using Knock.IntegrationTest.Requests;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Tecnicos;

/// <summary>
/// Spec 047, T-02 a T-05: validación, cuerpo ilegible, sesión y recurso inexistente de las rutas de servicios del técnico (SRV-02 a SRV-04) con MongoDB real; solo datos ficticios. Las corre el CI.
/// Una cuenta sin perfil de técnico recibe 404 <c>not_a_technician</c> (no 403 como dice CA-13): el código no revela si la cuenta tiene perfil.
/// </summary>
public sealed class Spec047ValidacionTests
{
    private const string _mine = "/api/v1/tecnicos/me/servicios";
    private const string _unreadable = "{ esto no es json";

    private static async Task<string> SeedTechnicianAsync(Spec020Host host)
    {
        var sub = Guid.NewGuid().ToString("D");
        await host.Database.GetCollection<BsonDocument>("tecnicos").InsertOneAsync(new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() }, { "usuarioId", sub }, { "nombre", "Demo Técnico" }, { "estado", "activo" }, { "oculto", false }, { "oficios", new BsonArray { Spec020Host.Plumbing } },
        });
        return sub;
    }

    private static Task<long> CountServicesAsync(Spec020Host host) =>
        host.Database.GetCollection<BsonDocument>("servicios_tecnico").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

    private static async Task<string> CreateServiceAsync(Spec020Host host, string sub)
    {
        using var created = await host.SendAsync(HttpMethod.Post, _mine, sub, new { nombre = "Cambio de llave", detalle = "Incluye materiales", precioReferencia = 350, oficio = Spec020Host.Plumbing });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await Spec020Host.ReadAsync(created)).GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task Crear_Con_Valor_Invalido_Responde_422_Sin_Repetirlo_Y_No_Guarda_CA_047_09()
    {
        await using var host = await Spec020Host.StartAsync();
        var sub = await SeedTechnicianAsync(host);
        const string badTrade = "Oficio Con Mayusculas";

        using var price = await host.SendAsync(HttpMethod.Post, _mine, sub, new { nombre = "Cambio de llave", precioReferencia = 0, oficio = Spec020Host.Plumbing });
        using var name = await host.SendAsync(HttpMethod.Post, _mine, sub, new { nombre = "   ", precioReferencia = 350, oficio = Spec020Host.Plumbing });
        using var trade = await host.SendAsync(HttpMethod.Post, _mine, sub, new { nombre = "Cambio de llave", precioReferencia = 350, oficio = badTrade });

        foreach (var (response, field) in new[] { (price, "precioReferencia"), (name, "nombre"), (trade, "oficio") })
        {
            response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            var body = await Spec020Host.ReadAsync(response);
            body.GetProperty("errors").GetProperty(field).GetArrayLength().Should().BeGreaterThan(0);
            Spec020Host.CarriesValue(body.GetRawText(), badTrade).Should().BeFalse("el error nunca repite el valor enviado");
        }

        (await CountServicesAsync(host)).Should().Be(0);
    }

    [Fact]
    public async Task Crear_Con_Cuerpo_Ilegible_Responde_400_Y_No_Guarda_CA_047_10()
    {
        await using var host = await Spec020Host.StartAsync();
        var sub = await SeedTechnicianAsync(host);

        using var response = await host.SendAsync(HttpMethod.Post, _mine, sub, rawJson: _unreadable);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CountServicesAsync(host)).Should().Be(0);
    }

    [Fact]
    public async Task Editar_Sin_Perfil_De_Tecnico_Responde_404_Not_A_Technician_Y_No_Cambia_Nada_CA_047_13()
    {
        await using var host = await Spec020Host.StartAsync();
        var owner = await SeedTechnicianAsync(host);
        var serviceId = await CreateServiceAsync(host, owner);

        // La spec dice 403; el código responde 404 con code not_a_technician a una cuenta sin perfil, igual que en las demás rutas del técnico.
        using var response = await host.SendAsync(HttpMethod.Put, $"{_mine}/{serviceId}", host.ClientSub, new { nombre = "Robado" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("not_a_technician");
        var stored = await host.Database.GetCollection<BsonDocument>("servicios_tecnico").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        stored["nombre"].AsString.Should().Be("Cambio de llave");
    }

    [Fact]
    public async Task Editar_Un_Servicio_Inexistente_Responde_404_Service_Not_Found_CA_047_16()
    {
        await using var host = await Spec020Host.StartAsync();
        var sub = await SeedTechnicianAsync(host);

        using var response = await host.SendAsync(HttpMethod.Put, $"{_mine}/{ObjectId.GenerateNewId()}", sub, new { nombre = "Nuevo nombre" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("service_not_found");
        (await CountServicesAsync(host)).Should().Be(0);
    }

    [Fact]
    public async Task Borrar_Sin_Token_Responde_401_Y_No_Borra_CA_047_18()
    {
        await using var host = await Spec020Host.StartAsync();
        var sub = await SeedTechnicianAsync(host);
        var serviceId = await CreateServiceAsync(host, sub);

        using var response = await host.SendAsync(HttpMethod.Delete, $"{_mine}/{serviceId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await CountServicesAsync(host)).Should().Be(1);
    }

    [Fact]
    public async Task Borrar_Un_Servicio_Inexistente_Responde_404_Service_Not_Found_CA_047_21()
    {
        await using var host = await Spec020Host.StartAsync();
        var sub = await SeedTechnicianAsync(host);
        await CreateServiceAsync(host, sub);

        using var response = await host.SendAsync(HttpMethod.Delete, $"{_mine}/{ObjectId.GenerateNewId()}", sub);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("service_not_found");
        (await CountServicesAsync(host)).Should().Be(1, "no se borra nada");
    }
}
