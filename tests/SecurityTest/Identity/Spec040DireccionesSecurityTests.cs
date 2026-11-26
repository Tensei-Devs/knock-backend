using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.SecurityTest.Identity;

/// <summary>Spec 040: sin credencial válida no se accede; una dirección ajena responde 404 sin revelar que existe y no cambia (R-14). Solo datos ficticios.</summary>
public class Spec040DireccionesSecurityTests
{
    private const string _route = "/api/v1/me/direcciones";
    private const string _collection = "direcciones";

    [Theory]
    [InlineData("GET", "")]
    [InlineData("POST", "")]
    [InlineData("PUT", "/665f1c2a9b3e4d0012345678")]
    [InlineData("DELETE", "/665f1c2a9b3e4d0012345678")]
    public async Task Sin_Token_O_Vencido_Responde_401(string method, string suffix)
    {
        var hosts = await SecurityHosts.SharedAsync();

        using var none = await SecureHttp.SendAsync(hosts.IdentityClient, new HttpMethod(method), _route + suffix);
        using var expired = await SecureHttp.SendAsync(hosts.IdentityClient, new HttpMethod(method), _route + suffix, bearer: TokenForge.Expired());

        await SecureHttp.AssertProblemAsync(none, HttpStatusCode.Unauthorized);
        await SecureHttp.AssertProblemAsync(expired, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Direccion_Ajena_Responde_404_Y_No_Cambia_CA_040_09_CA_040_15()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var (ownerSub, ownerId) = await SeedPersonAsync(hosts);
        var (attackerSub, _) = await SeedPersonAsync(hosts);
        var addresses = hosts.Identity.Database.GetCollection<BsonDocument>(_collection);
        var address = ObjectId.GenerateNewId();
        await addresses.InsertOneAsync(new BsonDocument
        {
            ["_id"] = address,
            ["usuarioId"] = ownerId,
            ["calleNumero"] = "cifrado-ficticio",
            ["colonia"] = "Tultengo",
            ["principal"] = false,
            ["creadoEn"] = DateTime.UtcNow,
        });
        var filter = Builders<BsonDocument>.Filter.Eq("_id", address);
        var before = await addresses.Find(filter).SingleAsync();
        var token = TokenForge.Valid(attackerSub);

        using var change = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Put, $"{_route}/{address}", JsonContent.Create(new { referencias = "Cambio ajeno" }), token);
        using var delete = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Delete, $"{_route}/{address}", bearer: token);

        await SecureHttp.AssertProblemAsync(change, HttpStatusCode.NotFound, address.ToString(), "cifrado-ficticio");
        await SecureHttp.AssertProblemAsync(delete, HttpStatusCode.NotFound, address.ToString(), "cifrado-ficticio");
        (await addresses.Find(filter).SingleAsync()).Should().Equal(before);

        // Control: la dueña sí puede borrarla, así que el 404 anterior se debe a la propiedad y no a un dato mal sembrado.
        using var own = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Delete, $"{_route}/{address}", bearer: TokenForge.Valid(ownerSub));
        own.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await addresses.CountDocumentsAsync(filter)).Should().Be(0);
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
}
