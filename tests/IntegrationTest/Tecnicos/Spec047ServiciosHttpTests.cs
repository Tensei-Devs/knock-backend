using System.Net;
using AwesomeAssertions;
using Knock.IntegrationTest.Requests;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Tecnicos;

/// <summary>Spec 047 (SRV-01 a SRV-04): servicios del técnico con MongoDB real. Solo datos ficticios.</summary>
public sealed class Spec047ServiciosHttpTests
{
    private const string _mine = "/api/v1/tecnicos/me/servicios";

    private static async Task<(ObjectId Id, string Sub)> SeedTechnicianAsync(Spec020Host host, string trade = Spec020Host.Plumbing, bool hidden = false)
    {
        var sub = Guid.NewGuid().ToString("D");
        var id = ObjectId.GenerateNewId();
        await host.Database.GetCollection<BsonDocument>("tecnicos").InsertOneAsync(new BsonDocument
        {
            { "_id", id }, { "usuarioId", sub }, { "nombre", "Demo Técnico" }, { "estado", "activo" }, { "oculto", hidden }, { "oficios", new BsonArray { trade } },
        });
        return (id, sub);
    }

    private static object Body(string trade = Spec020Host.Plumbing) => new { nombre = "Cambio de llave", detalle = "Incluye materiales", precioReferencia = 350, oficio = trade };

    [Fact]
    public async Task Crea_Lista_Edita_Y_Borra_Un_Servicio_Propio_CA_047_01_CA_047_06_CA_047_11_CA_047_17()
    {
        await using var host = await Spec020Host.StartAsync();
        var (id, sub) = await SeedTechnicianAsync(host);

        using var created = await host.SendAsync(HttpMethod.Post, _mine, sub, Body());
        var service = await Spec020Host.ReadAsync(created);
        var serviceId = service.GetProperty("id").GetString()!;
        using var listed = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{id}/servicios", Guid.NewGuid().ToString("D"));
        using var edited = await host.SendAsync(HttpMethod.Put, $"{_mine}/{serviceId}", sub, new { precioReferencia = 400 });
        using var deleted = await host.SendAsync(HttpMethod.Delete, $"{_mine}/{serviceId}", sub);
        using var again = await host.SendAsync(HttpMethod.Delete, $"{_mine}/{serviceId}", sub);

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        (await Spec020Host.ReadAsync(listed)).GetProperty("items").GetArrayLength().Should().Be(1);
        (await Spec020Host.ReadAsync(edited)).GetProperty("precioReferencia").GetInt32().Should().Be(400);
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        again.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Un_Servicio_Ajeno_Responde_404_Y_No_Cambia_CA_047_15_CA_047_20()
    {
        await using var host = await Spec020Host.StartAsync();
        var (_, owner) = await SeedTechnicianAsync(host);
        var (_, other) = await SeedTechnicianAsync(host);
        using var created = await host.SendAsync(HttpMethod.Post, _mine, owner, Body());
        var serviceId = (await Spec020Host.ReadAsync(created)).GetProperty("id").GetString()!;

        using var edit = await host.SendAsync(HttpMethod.Put, $"{_mine}/{serviceId}", other, new { nombre = "Robado" });
        using var delete = await host.SendAsync(HttpMethod.Delete, $"{_mine}/{serviceId}", other);

        edit.StatusCode.Should().Be(HttpStatusCode.NotFound);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await host.Database.GetCollection<BsonDocument>("servicios_tecnico").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(1);
    }

    [Fact]
    public async Task Oficio_Ajeno_Al_Tecnico_Responde_422_Y_El_51_Responde_409_CA_047_07_CA_047_26()
    {
        await using var host = await Spec020Host.StartAsync();
        var (id, sub) = await SeedTechnicianAsync(host);

        using var wrongTrade = await host.SendAsync(HttpMethod.Post, _mine, sub, Body(Spec020Host.Workshop));
        // Los 50 servicios se siembran en la base: crearlos por HTTP superaría las 30 peticiones por minuto de la política y respondería 429 (CA-29).
        await host.Database.GetCollection<BsonDocument>("servicios_tecnico").InsertManyAsync(Enumerable.Range(0, 50).Select(i => new BsonDocument
        {
            { "tecnicoId", id }, { "nombre", $"Servicio {i}" }, { "precioReferencia", 350 }, { "oficio", Spec020Host.Plumbing },
        }));

        using var overLimit = await host.SendAsync(HttpMethod.Post, _mine, sub, Body());

        wrongTrade.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        overLimit.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Tecnico_Oculto_Y_Sin_Sesion_CA_047_03_CA_047_04()
    {
        await using var host = await Spec020Host.StartAsync();
        var (hiddenId, _) = await SeedTechnicianAsync(host, hidden: true);

        using var hidden = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{hiddenId}/servicios", Guid.NewGuid().ToString("D"));
        using var anonymous = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{hiddenId}/servicios");

        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
