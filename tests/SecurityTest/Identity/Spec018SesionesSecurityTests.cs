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
        var (sub, userId) = await SeedPersonAsync(hosts);
        var session = await SeedSessionAsync(hosts, sub, userId);

        using var response = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Delete, $"{_route}/{session}");

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
        (await ReadAsync(hosts, session))["revocada"].AsBoolean.Should().BeFalse();
    }

    [Fact]
    public async Task Sesion_Ajena_Responde_El_Mismo_404_Que_Una_Inexistente_Y_No_Cambia()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var (ownerSub, ownerId) = await SeedPersonAsync(hosts);
        var (attackerSub, attackerId) = await SeedPersonAsync(hosts);
        var victim = await SeedSessionAsync(hosts, ownerSub, ownerId);
        var current = await SeedSessionAsync(hosts, ownerSub, ownerId);
        var attackerSession = await SeedSessionAsync(hosts, attackerSub, attackerId);
        var attacker = TokenForge.Valid(attackerSub, attackerSession.ToString());
        var before = await ReadAsync(hosts, victim);

        using var foreign = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Delete, $"{_route}/{victim}", bearer: attacker);
        using var missing = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Delete, $"{_route}/{ObjectId.GenerateNewId()}", bearer: attacker);

        var foreignBody = await SecureHttp.AssertProblemAsync(foreign, HttpStatusCode.NotFound, victim.ToString(), ownerSub);
        var missingBody = await SecureHttp.AssertProblemAsync(missing, HttpStatusCode.NotFound);
        SecureHttp.WithoutCorrelationId(foreignBody).Should().Be(SecureHttp.WithoutCorrelationId(missingBody));
        (await ReadAsync(hosts, victim)).Should().Equal(before);

        // Control: su dueña sí puede revocarla desde otra sesión, así que el 404 anterior se debe a la propiedad.
        using var own = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Delete, $"{_route}/{victim}", bearer: TokenForge.Valid(ownerSub, current.ToString()));
        own.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadAsync(hosts, victim))["revocada"].AsBoolean.Should().BeTrue();
    }

    private static async Task<(string Sub, ObjectId UserId)> SeedPersonAsync(SecurityHosts hosts)
    {
        var sub = TokenForge.Sub();
        var id = ObjectId.GenerateNewId();
        await hosts.Identity.Users.InsertOneAsync(new BsonDocument
        {
            ["_id"] = id,
            ["sub"] = sub,
            ["celularHash"] = new BsonBinaryData(Guid.NewGuid().ToByteArray()),
            ["estado"] = "activa",
            ["esTecnico"] = false,
            ["perfilCompleto"] = false,
            ["creadoEn"] = DateTime.UtcNow,
            ["version"] = 1,
        });
        return (sub, id);
    }

    private static async Task<ObjectId> SeedSessionAsync(SecurityHosts hosts, string sub, ObjectId userId)
    {
        var id = ObjectId.GenerateNewId();
        await hosts.Identity.Sessions.InsertOneAsync(new BsonDocument
        {
            ["_id"] = id,
            ["sub"] = sub,
            ["usuarioId"] = userId,
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
