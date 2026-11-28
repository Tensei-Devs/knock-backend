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
/// Spec 039 (R-22, CA-35) y spec 054 (T-09, CA-22): ruta interna <c>GET /api/v1/interno/cuentas/{usuarioId}/exportacion</c> de Mensajería, con MongoDB real y solo datos ficticios. Devuelve únicamente los
/// mensajes que escribió la persona (decisión de José del 2026-11-24) y solo responde a Identidad, con el permiso <c>exportacion</c> y un token de esa misma persona.
/// </summary>
public sealed class Spec039ExportacionMensajeriaTests
{
    private const string _identity = "knock-identity";

    private static string Route(string person) => $"/api/v1/interno/cuentas/{person}/exportacion";

    [Fact]
    public async Task Export_ReturnsOnlyTheMessagesTheCallerWrote_AsClientAndAsTechnician_CA_039_35()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        var name = "s039_exp_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var identityKey = RandomNumberGenerator.GetBytes(32);
        using var mongo = new MongoClient(fixture.ConnectionString);
        try
        {
            var me = Guid.NewGuid().ToString("D");
            var other = Guid.NewGuid().ToString("D");
            var mine = "mio-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
            var theirs = "ajeno-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
            var asClient = ObjectId.GenerateNewId();
            var asTechnician = ObjectId.GenerateNewId();
            var unrelated = ObjectId.GenerateNewId();
            var database = mongo.GetDatabase(name);
            await database.GetCollection<BsonDocument>("conversaciones").InsertManyAsync(
            [
                new BsonDocument { ["_id"] = asClient, ["clienteId"] = me, ["tecnicoUsuarioId"] = other },
                new BsonDocument { ["_id"] = asTechnician, ["clienteId"] = other, ["tecnicoUsuarioId"] = me },
                new BsonDocument { ["_id"] = unrelated, ["clienteId"] = other, ["tecnicoUsuarioId"] = Guid.NewGuid().ToString("D") },
            ]);
            await database.GetCollection<BsonDocument>("mensajes").InsertManyAsync(
            [
                Message(asClient, "cliente", me, mine),
                Message(asClient, "tecnico", other, theirs),
                Message(asTechnician, "tecnico", me, mine + "-t"),
                Message(asTechnician, "cliente", other, theirs + "-t"),
                Message(unrelated, "cliente", other, theirs + "-u"),
            ]);
            await using var host = await Spec037InternoAvisosSeguridadTests.CreateHostAsync(fixture.ConnectionString, name, RandomNumberGenerator.GetBytes(32), identityKey: identityKey);
            using var client = host.CreateClient();

            using var response = await SecureHttp.SendAsync(client, HttpMethod.Get, Route(me), bearer: Credential(identityKey, me));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var raw = await response.Content.ReadAsStringAsync();
            raw.Should().Contain(mine).And.Contain(mine + "-t");
            raw.Should().NotContain("ajeno-", "la exportación no lleva los mensajes de la otra parte");
            raw.Should().NotContain(other, "ni su identificador");
            raw.Should().NotContain("autorId");
            var conversations = System.Text.Json.JsonElement.Parse(raw).GetProperty("conversaciones");
            conversations.GetArrayLength().Should().Be(2, "solo las dos conversaciones de la persona");
            conversations.EnumerateArray().Select(c => c.GetProperty("rol").GetString()).Should().BeEquivalentTo("cliente", "tecnico");
            response.Headers.CacheControl?.NoStore.Should().BeTrue();
        }
        finally
        {
            await mongo.DropDatabaseAsync(name);
        }
    }

    [Fact]
    public async Task Export_RefusesAnythingButIdentityWithThePermissionAndThePersonOfTheRoute_CA_054_22()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        var name = "s039_expsec_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var identityKey = RandomNumberGenerator.GetBytes(32);
        var marketKey = RandomNumberGenerator.GetBytes(32);
        using var mongo = new MongoClient(fixture.ConnectionString);
        try
        {
            await using var host = await Spec037InternoAvisosSeguridadTests.CreateHostAsync(fixture.ConnectionString, name, marketKey, identityKey: identityKey);
            using var client = host.CreateClient();
            var me = Guid.NewGuid().ToString("D");

            using var anonymous = await SecureHttp.SendAsync(client, HttpMethod.Get, Route(me));
            using var userToken = await SecureHttp.SendAsync(client, HttpMethod.Get, Route(me), bearer: TokenForge.Valid(me));
            using var otherPerson = await SecureHttp.SendAsync(client, HttpMethod.Get, Route(me), bearer: Credential(identityKey, Guid.NewGuid().ToString("D")));
            using var noPermission = await SecureHttp.SendAsync(client, HttpMethod.Get, Route(me), bearer: Spec037InternoAvisosSeguridadTests.Mint(_identity, identityKey, "reporte_decidido", usuarioId: me));
            using var noPerson = await SecureHttp.SendAsync(client, HttpMethod.Get, Route(me), bearer: Spec037InternoAvisosSeguridadTests.Mint(_identity, identityKey, "exportacion"));
            using var fromMarket = await SecureHttp.SendAsync(client, HttpMethod.Get, Route(me), bearer: Spec037InternoAvisosSeguridadTests.Mint("knock-market", marketKey, "exportacion", usuarioId: me));
            using var foreignKey = await SecureHttp.SendAsync(client, HttpMethod.Get, Route(me), bearer: Credential(RandomNumberGenerator.GetBytes(32), me));

            anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            userToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "un token de persona no es un token de servicio");
            otherPerson.StatusCode.Should().Be(HttpStatusCode.Forbidden, "un token capturado no sirve para otra persona");
            noPermission.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            noPerson.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            fromMarket.StatusCode.Should().Be(HttpStatusCode.Forbidden, "solo Identidad exporta");
            foreignKey.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "una clave ajena no firma como Identidad");
            (await otherPerson.Content.ReadAsStringAsync()).Should().Contain("service_not_allowed");
        }
        finally
        {
            await mongo.DropDatabaseAsync(name);
        }
    }

    private static string Credential(byte[] identityKey, string person) =>
        Spec037InternoAvisosSeguridadTests.Mint(_identity, identityKey, "exportacion", usuarioId: person);

    private static BsonDocument Message(ObjectId conversation, string role, string author, string text) => new()
    {
        ["mensajeId"] = Guid.NewGuid().ToString("N"),
        ["conversacionId"] = conversation,
        ["autorRol"] = role,
        ["autorId"] = author,
        ["texto"] = text,
        ["contactoOculto"] = false,
        ["creadoEn"] = DateTime.UtcNow,
    };
}
