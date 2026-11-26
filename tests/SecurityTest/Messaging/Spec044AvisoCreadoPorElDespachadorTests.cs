using System.Net;
using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Knock.SecurityTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.SecurityTest.Messaging;

/// <summary>
/// Spec 044 T-11 (cierre del prerrequisito T-00) y spec 037 R-14: un evento aceptado por <c>POST /api/v1/interno/avisos</c> aparece en <c>GET /api/v1/avisos</c> y en el contador de quien lo recibe,
/// aunque no tenga dispositivos y con el push apagado; es de texto fijo, repetir el evento no lo duplica y nadie más lo ve. MongoDB real; solo datos ficticios.
/// </summary>
public sealed class Spec044AvisoCreadoPorElDespachadorTests
{
    private const string _internal = "/api/v1/interno/avisos";
    private const string _list = "/api/v1/avisos";
    private const string _counter = "/api/v1/avisos/no-leidos/contador";

    [Fact]
    public async Task AcceptedEvent_AppearsInTheListAndTheCounterOfTheRecipientOnly_WithoutDevices_R_14()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        var name = "s044_e2e_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var marketKey = RandomNumberGenerator.GetBytes(32);
        using var mongo = new MongoClient(fixture.ConnectionString);
        try
        {
            await using var host = await Spec037InternoAvisosSeguridadTests.CreateHostAsync(fixture.ConnectionString, name, marketKey);
            using var client = host.CreateClient();
            var person = Guid.NewGuid().ToString("D");
            var resource = ObjectId.GenerateNewId().ToString();
            var body = new { eventoId = Guid.NewGuid().ToString("N"), tipo = "cotizacion_nueva", destinatarioId = person, rol = "cliente", recurso = resource };
            var credential = Spec037InternoAvisosSeguridadTests.Mint("knock-market", marketKey, "cotizacion_nueva");

            using var first = await SecureHttp.PostJsonAsync(client, _internal, body, credential);
            using var repeated = await SecureHttp.PostJsonAsync(client, _internal, body, credential);
            using var list = await SecureHttp.SendAsync(client, HttpMethod.Get, _list, bearer: TokenForge.Valid(person));
            using var counter = await SecureHttp.SendAsync(client, HttpMethod.Get, _counter, bearer: TokenForge.Valid(person));
            using var stranger = await SecureHttp.SendAsync(client, HttpMethod.Get, _list, bearer: TokenForge.Valid(Guid.NewGuid().ToString("D")));

            first.StatusCode.Should().Be(HttpStatusCode.Accepted);
            repeated.StatusCode.Should().Be(HttpStatusCode.Accepted);
            list.StatusCode.Should().Be(HttpStatusCode.OK);
            var items = (await SecureHttp.ReadJsonAsync(list)).GetProperty("items");
            items.GetArrayLength().Should().Be(1, "repetir el evento no duplica el aviso");
            var notice = items[0];
            notice.GetProperty("tipo").GetString().Should().Be("cotizaciones");
            notice.GetProperty("comoRol").GetString().Should().Be("cliente");
            notice.GetProperty("titulo").GetString().Should().Be("Nueva cotización");
            notice.GetProperty("leido").GetBoolean().Should().BeFalse();
            notice.GetProperty("destino").GetProperty("id").GetString().Should().Be(resource);
            counter.StatusCode.Should().Be(HttpStatusCode.OK);
            (await SecureHttp.ReadJsonAsync(counter)).GetProperty("cantidad").GetInt32().Should().Be(1);
            (await SecureHttp.ReadJsonAsync(stranger)).GetProperty("items").GetArrayLength().Should().Be(0, "el aviso es solo de su destinatario");
            (await mongo.GetDatabase(name).GetCollection<BsonDocument>("notifications.enviados").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty))
                .Should().Be(0, "con el push apagado no se registra ningún envío, pero el aviso dentro de la app sí existe");
        }
        finally
        {
            await mongo.DropDatabaseAsync(name);
        }
    }

    [Fact]
    public async Task NoticesCreatedByTheDispatcher_PageMarkAsReadAndCountPerPerson_CA_044_01_CA_044_04_CA_044_05_CA_044_07_CA_044_09_CA_044_12()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        var name = "s044_ciclo_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var marketKey = RandomNumberGenerator.GetBytes(32);
        try
        {
            await using var host = await Spec037InternoAvisosSeguridadTests.CreateHostAsync(fixture.ConnectionString, name, marketKey);
            using var client = host.CreateClient();
            var person = Guid.NewGuid().ToString("D");
            var other = Guid.NewGuid().ToString("D");
            var credential = Spec037InternoAvisosSeguridadTests.Mint("knock-market", marketKey, "cotizacion_nueva");
            foreach (var recipient in new[] { person, person, person, other })
            {
                var body = new { eventoId = Guid.NewGuid().ToString("N"), tipo = "cotizacion_nueva", destinatarioId = recipient, rol = "cliente", recurso = ObjectId.GenerateNewId().ToString() };
                using var accepted = await SecureHttp.PostJsonAsync(client, _internal, body, credential);
                accepted.StatusCode.Should().Be(HttpStatusCode.Accepted);
            }

            var token = TokenForge.Valid(person);
            using var firstPage = await SecureHttp.SendAsync(client, HttpMethod.Get, _list + "?limite=2", bearer: token);
            var first = await SecureHttp.ReadJsonAsync(firstPage);
            first.GetProperty("items").GetArrayLength().Should().Be(2, "CA-04: el límite pedido se respeta");
            var cursor = first.GetProperty("siguienteCursor").GetString();
            cursor.Should().NotBeNullOrEmpty();
            using var secondPage = await SecureHttp.SendAsync(client, HttpMethod.Get, _list + "?limite=2&cursor=" + Uri.EscapeDataString(cursor), bearer: token);
            var second = await SecureHttp.ReadJsonAsync(secondPage);
            second.GetProperty("items").GetArrayLength().Should().Be(1, "la segunda página trae el aviso que falta sin repetir");
            var ownId = first.GetProperty("items")[0].GetProperty("id").GetString();

            using var strangerList = await SecureHttp.SendAsync(client, HttpMethod.Get, _list, bearer: TokenForge.Valid(other));
            var foreignId = (await SecureHttp.ReadJsonAsync(strangerList)).GetProperty("items")[0].GetProperty("id").GetString();

            using var read = await SecureHttp.SendAsync(client, HttpMethod.Post, $"{_list}/{ownId}/leido", bearer: token);
            using var readAgain = await SecureHttp.SendAsync(client, HttpMethod.Post, $"{_list}/{ownId}/leido", bearer: token);
            using var foreign = await SecureHttp.SendAsync(client, HttpMethod.Post, $"{_list}/{foreignId}/leido", bearer: token);
            read.StatusCode.Should().Be(HttpStatusCode.NoContent);
            readAgain.StatusCode.Should().Be(HttpStatusCode.NoContent, "CA-07: marcar dos veces es inocuo");
            foreign.StatusCode.Should().Be(HttpStatusCode.NotFound, "CA-09: el aviso de otra persona no existe para quien llama");
            await AssertUnreadAsync(client, token, 2);
            await AssertUnreadAsync(client, TokenForge.Valid(other), 1);

            using var all = await SecureHttp.SendAsync(client, HttpMethod.Post, _list + "/leidos", bearer: token);
            all.StatusCode.Should().Be(HttpStatusCode.NoContent);
            await AssertUnreadAsync(client, token, 0);
            await AssertUnreadAsync(client, TokenForge.Valid(other), 1);
        }
        finally
        {
            using var mongo = new MongoClient(fixture.ConnectionString);
            await mongo.DropDatabaseAsync(name);
        }
    }

    private static async Task AssertUnreadAsync(HttpClient client, string token, int expected)
    {
        using var response = await SecureHttp.SendAsync(client, HttpMethod.Get, _counter, bearer: token);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await SecureHttp.ReadJsonAsync(response)).GetProperty("cantidad").GetInt32().Should().Be(expected);
    }

    [Fact]
    public async Task SessionRevokedEvent_CreatesNoInAppNotice_R_14()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        var name = "s044_sid_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var identityKey = RandomNumberGenerator.GetBytes(32);
        using var mongo = new MongoClient(fixture.ConnectionString);
        try
        {
            await using var host = await Spec037InternoAvisosSeguridadTests.CreateHostAsync(fixture.ConnectionString, name, RandomNumberGenerator.GetBytes(32), identityKey: identityKey);
            using var client = host.CreateClient();
            var body = new { eventoId = Guid.NewGuid().ToString("N"), tipo = "sesion_revocada", sid = Guid.NewGuid().ToString("N") };

            using var response = await SecureHttp.PostJsonAsync(client, _internal, body, Spec037InternoAvisosSeguridadTests.Mint("knock-identity", identityKey, "sesion_revocada"));

            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            (await mongo.GetDatabase(name).GetCollection<BsonDocument>("avisos").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(0);
        }
        finally
        {
            await mongo.DropDatabaseAsync(name);
        }
    }

    [Fact]
    public async Task NoticesWithoutRole_AreSavedWithoutOne_AndShowUpForBothRoleFilters_AsSystemType_CA_044_16()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        var name = "s044_sinrol_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var identityKey = RandomNumberGenerator.GetBytes(32);
        using var mongo = new MongoClient(fixture.ConnectionString);
        try
        {
            var marketKey = RandomNumberGenerator.GetBytes(32);
            await using var host = await Spec037InternoAvisosSeguridadTests.CreateHostAsync(fixture.ConnectionString, name, marketKey, identityKey: identityKey);
            using var client = host.CreateClient();
            var person = Guid.NewGuid().ToString("D");
            var common = new { eventoId = Guid.NewGuid().ToString("N"), tipo = "reporte_decidido", destinatarioId = person, recurso = ObjectId.GenerateNewId().ToString() };
            var technicianOnly = new { eventoId = Guid.NewGuid().ToString("N"), tipo = "opinion_oculta", destinatarioId = person, rol = "tecnico", recurso = ObjectId.GenerateNewId().ToString() };
            var clientOnly = new { eventoId = Guid.NewGuid().ToString("N"), tipo = "cotizacion_nueva", destinatarioId = person, rol = "cliente", recurso = ObjectId.GenerateNewId().ToString() };
            using var first = await SecureHttp.PostJsonAsync(client, _internal, common, Spec037InternoAvisosSeguridadTests.Mint("knock-identity", identityKey, "reporte_decidido"));
            using var second = await SecureHttp.PostJsonAsync(client, _internal, technicianOnly, Spec037InternoAvisosSeguridadTests.Mint("knock-identity", identityKey, "opinion_oculta"));
            using var third = await SecureHttp.PostJsonAsync(client, _internal, clientOnly, Spec037InternoAvisosSeguridadTests.Mint("knock-market", marketKey, "cotizacion_nueva"));
            new[] { first, second, third }.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Accepted);

            var token = TokenForge.Valid(person);
            var asClient = await TitlesAsync(client, _list + "?comoRol=cliente", token);
            var asTechnician = await TitlesAsync(client, _list + "?comoRol=tecnico", token);
            var unfiltered = await TitlesAsync(client, _list, token);

            asClient.Should().BeEquivalentTo("Reporte revisado", "Nueva cotización");
            asTechnician.Should().BeEquivalentTo("Reporte revisado", "Opinión oculta");
            unfiltered.Should().HaveCount(3);
            var stored = await mongo.GetDatabase(name).GetCollection<BsonDocument>("avisos").Find(new BsonDocument("eventoId", common.eventoId)).SingleAsync();
            stored["comoRol"].IsBsonNull.Should().BeTrue("el aviso común no pertenece a un rol");
            stored["tipo"].AsString.Should().Be("sistema");
            await AssertUnreadAsync(client, token, 3);
        }
        finally
        {
            await mongo.DropDatabaseAsync(name);
        }
    }

    private static async Task<string[]> TitlesAsync(HttpClient client, string path, string token)
    {
        using var response = await SecureHttp.SendAsync(client, HttpMethod.Get, path, bearer: token);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return [.. (await SecureHttp.ReadJsonAsync(response)).GetProperty("items").EnumerateArray().Select(n => n.GetProperty("titulo").GetString()!)];
    }
}
