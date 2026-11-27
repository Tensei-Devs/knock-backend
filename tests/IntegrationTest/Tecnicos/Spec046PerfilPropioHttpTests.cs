using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Requests;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Tecnicos;

/// <summary><c>GET /tecnicos/me</c> (spec 046, TEC-15) sobre MongoDB real, con técnicos registrados por <c>POST /tecnicos</c>. Solo datos ficticios.</summary>
public sealed class Spec046PerfilPropioHttpTests
{
    private const string _route = "/api/v1/tecnicos/me";
    private const string _workshop = "Avenida Inventada 456, Colonia Piloto";

    private static readonly string[] _fields =
    [
        "id", "nombre", "oficios", "otroOficio", "atencion", "radioKm", "coloniaId", "nombreNegocio", "descripcion", "anios", "estado", "verificacion",
        "disponibleHoy", "creadoEn", "fotoUrl", "direccionTaller",
    ];

    private static async Task<(string Sub, string Id)> RegisterAsync(Spec020Host host, string attention, string name = "Ana Pruebas")
    {
        var sub = Guid.NewGuid().ToString("D");
        var body = new Dictionary<string, object?>
        {
            ["nombre"] = name,
            ["oficios"] = new[] { Spec020Host.Plumbing },
            ["atencion"] = attention,
            ["coloniaId"] = host.ColoniaId.ToString(),
            ["aceptaTerminos"] = true,
        };
        if (attention == "taller")
        {
            body["direccionTaller"] = _workshop;
        }
        else
        {
            body["radioKm"] = 5;
        }

        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/tecnicos", sub, body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (sub, (await Spec020Host.ReadAsync(response)).GetProperty("id").GetString()!);
    }

    private static async Task<JsonElement> ReadOwnAsync(Spec020Host host, string sub, string path = _route)
    {
        using var response = await host.SendAsync(HttpMethod.Get, path, sub);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.CacheControl.Private.Should().BeTrue();
        return await Spec020Host.ReadAsync(response);
    }

    private static Task<List<BsonDocument>> TechniciansAsync(Spec020Host host) =>
        host.Database.GetCollection<BsonDocument>("tecnicos").Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(Builders<BsonDocument>.Sort.Ascending("_id")).ToListAsync();

    [Fact]
    public async Task El_Tecnico_Lee_Su_Perfil_Con_Los_Campos_Del_Contrato_Y_Nada_Mas_CA_046_70_CA_046_74()
    {
        await using var host = await Spec020Host.StartAsync();
        var (sub, id) = await RegisterAsync(host, "taller");
        var before = await TechniciansAsync(host);
        var profile = default(JsonElement);

        var logs = await ConsoleCapture.RunAsync(async () => profile = await ReadOwnAsync(host, sub));

        profile.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(_fields, "nunca usuarioId, teléfono, contacto, ubicación ni ocultamiento");
        profile.GetProperty("id").GetString().Should().Be(id);
        profile.GetProperty("nombre").GetString().Should().Be("Ana Pruebas");
        profile.GetProperty("coloniaId").GetString().Should().Be(host.ColoniaId.ToString());
        profile.GetProperty("fotoUrl").ValueKind.Should().Be(JsonValueKind.Null);
        profile.GetProperty("direccionTaller").GetString().Should().Be(_workshop);
        profile.GetRawText().Should().NotContain(sub);
        logs.Should().NotContain("Inventada");
        (await TechniciansAsync(host)).Should().Equal(before, "leer no cambia nada");
        before.Should().ContainSingle().Which["tallerDireccion"].AsString.Should().NotContain("Inventada", "se guarda cifrada");
    }

    [Fact]
    public async Task Un_Tecnico_A_Domicilio_No_Recibe_Direccion_CA_046_73()
    {
        await using var host = await Spec020Host.StartAsync();
        var (sub, _) = await RegisterAsync(host, "domicilio");

        var profile = await ReadOwnAsync(host, sub);

        profile.GetProperty("direccionTaller").ValueKind.Should().Be(JsonValueKind.Null);
        profile.GetProperty("radioKm").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task Los_Identificadores_Enviados_No_Dan_El_Perfil_Ajeno_Y_Cada_Quien_Ve_El_Suyo_CA_046_70()
    {
        await using var host = await Spec020Host.StartAsync();
        var (ownerSub, ownerId) = await RegisterAsync(host, "taller", "Elena Pruebas");
        var (otherSub, otherId) = await RegisterAsync(host, "domicilio", "Berta Pruebas");
        var before = await TechniciansAsync(host);

        var attempt = await ReadOwnAsync(host, otherSub, $"{_route}?id={ownerId}&tecnicoId={ownerId}&usuarioId={ownerSub}");
        var control = await ReadOwnAsync(host, ownerSub);

        attempt.GetProperty("id").GetString().Should().Be(otherId);
        attempt.GetRawText().Should().NotContain(ownerId).And.NotContain("Inventada");
        control.GetProperty("id").GetString().Should().Be(ownerId);
        control.GetProperty("direccionTaller").GetString().Should().Be(_workshop);
        (await TechniciansAsync(host)).Should().Equal(before);
    }

    [Fact]
    public async Task Una_Cuenta_Sin_Perfil_De_Tecnico_Recibe_404_Y_Nada_Cambia_CA_046_72()
    {
        await using var host = await Spec020Host.StartAsync();
        await RegisterAsync(host, "taller");
        var before = await TechniciansAsync(host);

        using var response = await host.SendAsync(HttpMethod.Get, _route, host.ClientSub);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadAsStringAsync()).Should().NotContain("Inventada");
        (await TechniciansAsync(host)).Should().Equal(before);
    }

    [Fact]
    public async Task Suspendido_U_Oculto_Puede_Leer_Su_Perfil_CA_046_70()
    {
        await using var host = await Spec020Host.StartAsync();
        var (sub, id) = await RegisterAsync(host, "domicilio");
        await host.Database.GetCollection<BsonDocument>("tecnicos").UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("usuarioId", sub),
            Builders<BsonDocument>.Update.Set("estado", "suspendido").Set("oculto", true));

        var profile = await ReadOwnAsync(host, sub);

        profile.GetProperty("id").GetString().Should().Be(id);
        profile.GetProperty("estado").GetString().Should().Be("suspendido");
    }

    [Fact]
    public async Task Despues_De_Guardar_Con_Put_Se_Leen_Los_Valores_Nuevos_CA_046_76()
    {
        await using var host = await Spec020Host.StartAsync();
        var (sub, _) = await RegisterAsync(host, "taller");
        await ReadOwnAsync(host, sub);

        using var updated = await host.SendAsync(HttpMethod.Put, _route, sub,
            new { descripcion = "Descripción nueva", direccionTaller = "Calzada Imaginaria 7, Colonia Piloto" });
        var profile = await ReadOwnAsync(host, sub);

        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Spec020Host.ReadAsync(updated)).TryGetProperty("direccionTaller", out _).Should().BeFalse("la respuesta del PUT no cambia");
        profile.GetProperty("descripcion").GetString().Should().Be("Descripción nueva");
        profile.GetProperty("direccionTaller").GetString().Should().Be("Calzada Imaginaria 7, Colonia Piloto");
    }
}
