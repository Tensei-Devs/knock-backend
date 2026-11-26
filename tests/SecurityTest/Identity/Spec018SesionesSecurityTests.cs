using System.Net;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.SecurityTest.Identity;

/// <summary>Spec 018: sin credencial válida no se lista ni se revoca; una sesión ajena responde 404, igual que una inexistente, y no cambia. Solo datos ficticios.</summary>
public class Spec018SesionesSecurityTests
{
    private const string _route = "/api/v1/me/sesiones";

    [Fact]
    public async Task Sin_Token_O_Vencido_No_Lista_CA_018_08()
    {
        var hosts = await SecurityHosts.SharedAsync();

        using var none = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Get, _route);
        using var expired = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Get, _route, bearer: TokenForge.Expired());

        await SecureHttp.AssertProblemAsync(none, HttpStatusCode.Unauthorized);
        await SecureHttp.AssertProblemAsync(expired, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sin_Token_No_Revoca_CA_018_08()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var session = await SeedSessionAsync(hosts, TokenForge.Sub());

        using var response = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Delete, $"{_route}/{session}");

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
        (await ReadAsync(hosts, session))["revocada"].AsBoolean.Should().BeFalse();
    }

    [Fact]
    public async Task Sesion_Ajena_Responde_404_Y_No_Cambia()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var owner = TokenForge.Sub();
        var attacker = TokenForge.Sub();
        var victim = await SeedSessionAsync(hosts, owner);
        var own = await SeedSessionAsync(hosts, attacker);
        var token = TokenForge.Valid(attacker, own.ToString());
        var before = await ReadAsync(hosts, victim);

        using var foreign = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Delete, $"{_route}/{victim}", bearer: token);
        using var missing = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Delete, $"{_route}/{ObjectId.GenerateNewId()}", bearer: token);

        await SecureHttp.AssertProblemAsync(foreign, HttpStatusCode.NotFound, victim.ToString(), owner);
        await SecureHttp.AssertProblemAsync(missing, HttpStatusCode.NotFound);
        (await ReadAsync(hosts, victim)).Should().Equal(before);
    }

    private static async Task<ObjectId> SeedSessionAsync(SecurityHosts hosts, string sub)
    {
        var id = ObjectId.GenerateNewId();
        await hosts.Identity.Sessions.InsertOneAsync(new BsonDocument
        {
            ["_id"] = id,
            ["sub"] = sub,
            ["deviceId"] = Guid.NewGuid().ToString("N"),
            ["dispositivo"] = "Telefono de prueba",
            ["huellaRenovacion"] = Guid.NewGuid().ToString("N"),
            ["revocada"] = false,
            ["creadoEn"] = DateTime.UtcNow,
            ["ultimaActividad"] = DateTime.UtcNow,
            ["venceEn"] = DateTime.UtcNow.AddDays(1),
        });
        return id;
    }

    private static async Task<BsonDocument> ReadAsync(SecurityHosts hosts, ObjectId id) =>
        await hosts.Identity.Sessions.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).SingleAsync();
}
