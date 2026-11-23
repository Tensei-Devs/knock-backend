using System.Net;
using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Knock.SecurityTest.Support;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.SecurityTest.Messaging;

/// <summary>
/// Spec 061 (R-07a, CA-09a): el aviso <c>opinion_oculta</c> que Mercado entrega a <c>POST /api/v1/interno/avisos</c> es de texto fijo. Mensajería lo acepta solo para el rol técnico, una sola vez por
/// <c>eventoId</c>, con el permiso <c>opinion_oculta</c>, y rechaza cualquier motivo o dato de la opinión sin repetirlo. MongoDB real; solo datos ficticios.
/// </summary>
public sealed class Spec061AvisoTecnicoTests
{
    private const string _path = "/api/v1/interno/avisos";
    private const string _type = "opinion_oculta";

    private static string Mint(byte[] key, string permission)
    {
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "knock-market",
            Audience = "knock-notifications",
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(4),
            Claims = new Dictionary<string, object> { ["permisos"] = permission, ["jti"] = Guid.NewGuid().ToString("N") },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256),
        });
    }

    private static Task<SecurityMessagingFactory> CreateHostAsync(string connectionString, string databaseName, byte[] marketKey) =>
        SecurityMessagingFactory.CreateAsync(new Dictionary<string, string?>
        {
            ["ConnectionStrings:KnockMongo"] = connectionString,
            ["Mongo:Databases:Messaging"] = databaseName,
            ["Security:InternalTokens:MarketKey"] = Convert.ToBase64String(marketKey),
            ["Security:InternalTokens:IdentityKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        });

    [Fact]
    public async Task Aviso_Al_Tecnico_Se_Acepta_Una_Sola_Vez_Y_No_Guarda_Opinion_Ni_Motivo_CA_061_09a()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        var name = "s061_aviso_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var marketKey = RandomNumberGenerator.GetBytes(32);
        using var mongo = new MongoClient(fixture.ConnectionString);
        try
        {
            await using var host = await CreateHostAsync(fixture.ConnectionString, name, marketKey);
            using var client = host.CreateClient();
            var opinion = ObjectId.GenerateNewId().ToString();
            var body = new { eventoId = "OpinionModerada:" + opinion + ":1", tipo = _type, destinatarioId = Guid.NewGuid().ToString("D"), rol = "tecnico", recurso = opinion };
            var credential = Mint(marketKey, _type);
            var notices = mongo.GetDatabase(name).GetCollection<BsonDocument>("notifications.enviados");

            using var first = await SecureHttp.PostJsonAsync(client, _path, body, credential);
            using var again = await SecureHttp.PostJsonAsync(client, _path, body, credential);

            first.StatusCode.Should().Be(HttpStatusCode.Accepted);
            again.StatusCode.Should().Be(HttpStatusCode.Accepted);
            var stored = await notices.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
            stored.Should().ContainSingle("el receptor es idempotente por eventoId");
            stored[0].ToJson().Should().NotContain(opinion, "el registro de envíos no guarda identificadores de recurso").And.NotContain("motivo").And.NotContain("comentario");
        }
        finally
        {
            await mongo.DropDatabaseAsync(name);
        }
    }

    [Fact]
    public async Task Aviso_Con_Motivo_Rol_Cliente_O_Sin_Permiso_Se_Rechaza_Sin_Repetir_El_Texto_CA_061_09a()
    {
        var marketKey = RandomNumberGenerator.GetBytes(32);
        await using var host = await SecurityMessagingFactory.CreateAsync(new Dictionary<string, string?>
        {
            ["Security:InternalTokens:MarketKey"] = Convert.ToBase64String(marketKey),
            ["Security:InternalTokens:IdentityKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        });
        using var client = host.CreateClient();
        var opinion = ObjectId.GenerateNewId().ToString();
        var reason = "motivo-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
        var credential = Mint(marketKey, _type);
        static string Id() => "OpinionModerada:" + Guid.NewGuid().ToString("N");

        using var withReason = await SecureHttp.PostJsonAsync(
            client, _path, new { eventoId = Id(), tipo = _type, destinatarioId = Guid.NewGuid().ToString("D"), rol = "tecnico", recurso = opinion, motivo = reason }, credential);
        using var withText = await SecureHttp.PostJsonAsync(
            client, _path, new { eventoId = Id(), tipo = _type, destinatarioId = Guid.NewGuid().ToString("D"), rol = "tecnico", recurso = opinion, texto = reason }, credential);
        using var clientRole = await SecureHttp.PostJsonAsync(
            client, _path, new { eventoId = Id(), tipo = _type, destinatarioId = Guid.NewGuid().ToString("D"), rol = "cliente", recurso = opinion }, credential);
        using var otherPermission = await SecureHttp.PostJsonAsync(
            client, _path, new { eventoId = Id(), tipo = _type, destinatarioId = Guid.NewGuid().ToString("D"), rol = "tecnico", recurso = opinion }, Mint(marketKey, "solicitud_nueva"));

        withReason.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        withText.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        clientRole.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        otherPermission.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await withReason.Content.ReadAsStringAsync()).Should().NotContain(reason);
        (await withText.Content.ReadAsStringAsync()).Should().NotContain(reason);
    }
}
