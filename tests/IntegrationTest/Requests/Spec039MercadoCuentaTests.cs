using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>
/// Spec 039b (R-22, R-24 y R-25): las rutas internas de la cuenta en Mercado (<c>/api/v1/interno/cuentas/{usuarioId}/...</c>) que solo llama Identidad con su token de servicio
/// (permiso <c>cuenta</c>): pendientes, cancelación al programar la baja, exportación y el consumo de <c>CuentaEliminada</c>. MongoDB real; solo datos ficticios.
/// </summary>
public sealed class Spec039MercadoCuentaTests
{
    private static readonly Dictionary<string, string?> _settings = new() { ["Security:InternalTokens:IdentityKey"] = InternalServiceTokens.KeyBase64 };

    private static async Task<Spec020Host> StartAsync() => await Spec020Host.StartAsync(_settings);

    private static string Token(string person, string permission = "cuenta") => InternalServiceTokens.Issue(person, "sin-elemento", [permission]);

    private static async Task<HttpResponseMessage> SendAsync(Spec020Host host, HttpMethod method, string person, string action, string? token)
    {
        using var request = new HttpRequestMessage(method, new Uri($"/api/v1/interno/cuentas/{person}/{action}", UriKind.Relative));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await host.Client.SendAsync(request);
    }

    private static IMongoCollection<BsonDocument> Collection(Spec020Host host, string name) => host.Database.GetCollection<BsonDocument>(name);

    private static async Task<ObjectId> SeedRequestAsync(Spec020Host host, string client, string state)
    {
        var id = ObjectId.GenerateNewId();
        await Collection(host, "solicitudes").InsertOneAsync(new BsonDocument
        {
            { "_id", id }, { "clienteId", client }, { "oficioClave", "mec" }, { "oficioNombre", "Mecánica" }, { "modalidad", "domicilio" },
            { "descripcion", "Descripción privada de prueba" }, { "fotos", new BsonArray { "foto-prueba" } }, { "direccionCifrada", "1.cifrada" },
            { "estado", state }, { "venceEn", DateTime.UtcNow.AddDays(2) }, { "creadoEn", DateTime.UtcNow }, { "actualizadoEn", DateTime.UtcNow },
        });
        return id;
    }

    private static async Task<ObjectId> SeedQuoteAsync(Spec020Host host, ObjectId request, string technician, string state)
    {
        var id = ObjectId.GenerateNewId();
        await Collection(host, "cotizaciones").InsertOneAsync(new BsonDocument
        {
            { "_id", id }, { "solicitudId", request }, { "tecnicoId", ObjectId.GenerateNewId() }, { "tecnicoUsuarioId", technician }, { "tipoPrecio", "fijo" },
            { "precio", 500 }, { "tope", 500 }, { "dia", "2026-12-01" }, { "hora", "10:00" }, { "nota", "Nota privada de prueba" }, { "estado", state }, { "creadoEn", DateTime.UtcNow },
        });
        return id;
    }

    private static async Task<ObjectId> SeedVisitAsync(Spec020Host host, string client, string technician, string state)
    {
        var id = ObjectId.GenerateNewId();
        await Collection(host, "visitas").InsertOneAsync(new BsonDocument
        {
            { "_id", id }, { "solicitudId", ObjectId.GenerateNewId() }, { "cotizacionId", ObjectId.GenerateNewId() }, { "clienteId", client }, { "tecnicoUsuarioId", technician },
            { "tecnicoId", ObjectId.GenerateNewId() }, { "modalidad", "domicilio" }, { "dia", "2026-12-01" }, { "hora", "10:00" }, { "precio", 500 }, { "tope", 500 },
            { "estado", state }, { "codigoCifrado", "1.codigo-cifrado" }, { "creadoEn", DateTime.UtcNow },
        });
        return id;
    }

    [Fact]
    public async Task Pending_ReturnsOnlyTheIdsOfThatPerson_CA_039_37()
    {
        await using var host = await StartAsync();
        var person = Guid.NewGuid().ToString("D");
        var other = Guid.NewGuid().ToString("D");
        var mine = await SeedVisitAsync(host, person, other, "confirmada");
        var asTechnician = await SeedVisitAsync(host, other, person, "confirmada");
        await SeedVisitAsync(host, other, other, "confirmada");
        await SeedVisitAsync(host, person, other, "terminada");
        var quote = await SeedQuoteAsync(host, await SeedRequestAsync(host, other, "cotizando"), person, "enviada");
        await SeedQuoteAsync(host, await SeedRequestAsync(host, other, "cotizando"), person, "rechazada");
        await SeedQuoteAsync(host, await SeedRequestAsync(host, other, "cotizando"), other, "enviada");

        using var response = await SendAsync(host, HttpMethod.Get, person, "pendientes", Token(person));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var json = JsonElement.Parse(await response.Content.ReadAsStringAsync());
        json.GetProperty("appointmentTotal").GetInt32().Should().Be(2);
        json.GetProperty("appointmentIds").EnumerateArray().Select(x => x.GetString()).Should().BeEquivalentTo(mine.ToString(), asTechnician.ToString());
        json.GetProperty("quoteTotal").GetInt32().Should().Be(1);
        json.GetProperty("quoteIds").EnumerateArray().Select(x => x.GetString()).Should().Equal(quote.ToString());
        json.GetProperty("visitInProgress").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Pending_ReportsAVisitInProgress_CA_039_38()
    {
        await using var host = await StartAsync();
        var person = Guid.NewGuid().ToString("D");
        await SeedVisitAsync(host, person, Guid.NewGuid().ToString("D"), "en_curso");

        using var response = await SendAsync(host, HttpMethod.Get, person, "pendientes", Token(person));

        JsonElement.Parse(await response.Content.ReadAsStringAsync()).GetProperty("visitInProgress").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task TheRoutes_RefuseAnotherPersonTheWrongPermissionAndNoToken_CA_039_40()
    {
        await using var host = await StartAsync();
        var person = Guid.NewGuid().ToString("D");
        var intruder = Guid.NewGuid().ToString("D");

        using var anotherPerson = await SendAsync(host, HttpMethod.Get, person, "pendientes", Token(intruder));
        using var wrongPermission = await SendAsync(host, HttpMethod.Get, person, "exportacion", Token(person, "historial"));
        using var noToken = await SendAsync(host, HttpMethod.Post, person, "eliminada", null);

        anotherPerson.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        wrongPermission.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        noToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CancelOpen_ClosesTheOpenRequestsAndWithdrawsThePendingQuotes_AndLeavesAnOutboxNotice_R_25()
    {
        await using var host = await StartAsync();
        var person = Guid.NewGuid().ToString("D");
        var other = Guid.NewGuid().ToString("D");
        var open = await SeedRequestAsync(host, person, "enviada");
        var finished = await SeedRequestAsync(host, person, "terminada");
        var untouched = await SeedRequestAsync(host, other, "enviada");
        var sentQuote = await SeedQuoteAsync(host, untouched, person, "enviada");

        using var response = await SendAsync(host, HttpMethod.Post, person, "cancelaciones", Token(person));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Collection(host, "solicitudes").Find(Builders<BsonDocument>.Filter.Eq("_id", open)).SingleAsync())["estado"].AsString.Should().Be("cancelada");
        (await Collection(host, "solicitudes").Find(Builders<BsonDocument>.Filter.Eq("_id", finished)).SingleAsync())["estado"].AsString.Should().Be("terminada");
        (await Collection(host, "solicitudes").Find(Builders<BsonDocument>.Filter.Eq("_id", untouched)).SingleAsync())["estado"].AsString.Should().Be("enviada");
        (await Collection(host, "cotizaciones").Find(Builders<BsonDocument>.Filter.Eq("_id", sentQuote)).SingleAsync())["estado"].AsString.Should().Be("retirada");
        (await Collection(host, "outbox").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id", $"SolicitudCancelada:{open}"))).Should().Be(1);
        (await Collection(host, "outbox").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id", $"CotizacionRetirada:{sentQuote}"))).Should().Be(1);
    }

    [Fact]
    public async Task Export_ReturnsOnlyTheOwnDataWithoutAddressesCodesOrPhotos_R_22()
    {
        await using var host = await StartAsync();
        var person = Guid.NewGuid().ToString("D");
        var other = Guid.NewGuid().ToString("D");
        var own = await SeedRequestAsync(host, person, "enviada");
        var foreign = await SeedRequestAsync(host, other, "enviada");
        var visit = await SeedVisitAsync(host, person, other, "confirmada");

        using var response = await SendAsync(host, HttpMethod.Get, person, "exportacion", Token(person));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        var raw = await response.Content.ReadAsStringAsync();
        var json = JsonElement.Parse(raw);
        json.GetProperty("solicitudes").EnumerateArray().Select(x => x.GetProperty("id").GetString()).Should().Equal(own.ToString());
        json.GetProperty("visitas").EnumerateArray().Select(x => x.GetProperty("id").GetString()).Should().Equal(visit.ToString());
        json.GetProperty("solicitudes")[0].EnumerateObject().Select(property => property.Name).Should().NotContain(["direccionCifrada", "fotos", "clienteId"]);
        json.GetProperty("visitas")[0].EnumerateObject().Select(property => property.Name).Should().NotContain(["codigoCifrado", "clienteId", "tecnicoUsuarioId"]);
        raw.Should().NotContain(foreign.ToString()).And.NotContain(other);
    }

    [Fact]
    public async Task AccountDeleted_CancelsConfirmedVisitsAsLateAndWipesThePersonalData_AndRepeatingItChangesNothing_CA_039_38()
    {
        await using var host = await StartAsync();
        var person = Guid.NewGuid().ToString("D");
        var other = Guid.NewGuid().ToString("D");
        var visit = await SeedVisitAsync(host, person, other, "confirmada");
        var request = await SeedRequestAsync(host, person, "terminada");
        var openRequest = await SeedRequestAsync(host, person, "enviada");
        var quote = await SeedQuoteAsync(host, await SeedRequestAsync(host, other, "cotizando"), person, "aceptada");
        var otherRequest = await SeedRequestAsync(host, other, "terminada");

        using var first = await SendAsync(host, HttpMethod.Post, person, "eliminada", Token(person));
        using var second = await SendAsync(host, HttpMethod.Post, person, "eliminada", Token(person));

        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        second.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var cancelled = await Collection(host, "visitas").Find(Builders<BsonDocument>.Filter.Eq("_id", visit)).SingleAsync();
        cancelled["estado"].AsString.Should().Be("cancelada");
        cancelled["tardia"].AsBoolean.Should().BeTrue();
        cancelled["cancelacion"]["por"].AsString.Should().Be("cliente");
        (await Collection(host, "outbox").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id", $"CancelacionTardia:{visit}"))).Should().Be(1);
        var wiped = await Collection(host, "solicitudes").Find(Builders<BsonDocument>.Filter.Eq("_id", request)).SingleAsync();
        wiped["descripcion"].AsString.Should().BeEmpty();
        wiped.Contains("direccionCifrada").Should().BeFalse();
        wiped["fotos"].AsBsonArray.Should().BeEmpty();
        (await Collection(host, "solicitudes").Find(Builders<BsonDocument>.Filter.Eq("_id", openRequest)).SingleAsync())["estado"].AsString.Should().Be("cancelada");
        (await Collection(host, "cotizaciones").Find(Builders<BsonDocument>.Filter.Eq("_id", quote)).SingleAsync()).Contains("nota").Should().BeFalse();
        (await Collection(host, "solicitudes").Find(Builders<BsonDocument>.Filter.Eq("_id", otherRequest)).SingleAsync())["descripcion"].AsString.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AccountDeleted_HidesAndAnonymizesTheTechnicianProfile_R_25()
    {
        await using var host = await StartAsync();
        var person = Guid.NewGuid().ToString("D");
        var technician = ObjectId.GenerateNewId();
        await Collection(host, "tecnicos").InsertOneAsync(new BsonDocument
        {
            { "_id", technician }, { "usuarioId", person }, { "nombre", "Nombre de prueba" }, { "nombreNegocio", "Negocio de prueba" }, { "descripcion", "Texto de prueba" },
            { "estado", "activo" }, { "oculto", false }, { "oficios", new BsonArray { "mec" } },
        });
        await Collection(host, "fotos_trabajos").InsertOneAsync(new BsonDocument { { "_id", ObjectId.GenerateNewId() }, { "tecnicoId", technician }, { "url", "foto-de-prueba" } });

        using var response = await SendAsync(host, HttpMethod.Post, person, "eliminada", Token(person));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var profile = await Collection(host, "tecnicos").Find(Builders<BsonDocument>.Filter.Eq("_id", technician)).SingleAsync();
        profile["estado"].AsString.Should().Be("eliminado");
        profile["oculto"].AsBoolean.Should().BeTrue();
        profile.Contains("nombreNegocio").Should().BeFalse();
        profile.Contains("descripcion").Should().BeFalse();
        profile["nombre"].AsString.Should().NotBe("Nombre de prueba");
        (await Collection(host, "fotos_trabajos").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("tecnicoId", technician))).Should().Be(0);
    }
}
