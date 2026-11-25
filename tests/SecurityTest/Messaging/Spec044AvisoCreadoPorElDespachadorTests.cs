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
}
