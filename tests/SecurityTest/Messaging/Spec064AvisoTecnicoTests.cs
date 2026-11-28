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
/// Spec 064 (R-07, R-08, D-01, CA-08): el aviso <c>tecnico_no_se_presento</c> que Mercado entrega a <c>POST /api/v1/interno/avisos</c> es de texto fijo. Mensajería lo acepta solo para el rol técnico,
/// una sola vez por <c>eventoId</c> y solo con el permiso del tipo; rechaza cualquier dato extra sin repetirlo. MongoDB real; solo datos ficticios.
/// </summary>
public sealed class Spec064AvisoTecnicoTests
{
    private const string _path = "/api/v1/interno/avisos";
    private const string _type = "tecnico_no_se_presento";

    [Fact]
    public async Task Aviso_Se_Acepta_Una_Sola_Vez_Y_No_Guarda_La_Visita_CA_064_08()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        var name = "s064_aviso_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var marketKey = RandomNumberGenerator.GetBytes(32);
        using var mongo = new MongoClient(fixture.ConnectionString);
        try
        {
            await Spec037InternoAvisosSeguridadTests.EnsureDeduplicationIndexAsync(mongo, name);
            await using var host = await Spec037InternoAvisosSeguridadTests.CreateHostAsync(fixture.ConnectionString, name, marketKey, pushEnabled: true);
            using var client = host.CreateClient();
            var visit = ObjectId.GenerateNewId().ToString();
            var body = new { eventoId = "TecnicoNoSePresento:" + Guid.NewGuid().ToString("N"), tipo = _type, destinatarioId = Guid.NewGuid().ToString("D"), rol = "tecnico", recurso = visit };
            var credential = Spec037InternoAvisosSeguridadTests.Mint("knock-market", marketKey, _type);
            var notices = mongo.GetDatabase(name).GetCollection<BsonDocument>("notifications.enviados");

            using var first = await SecureHttp.PostJsonAsync(client, _path, body, credential);
            using var again = await SecureHttp.PostJsonAsync(client, _path, body, credential);

            first.StatusCode.Should().Be(HttpStatusCode.Accepted);
            again.StatusCode.Should().Be(HttpStatusCode.Accepted);
            var stored = await notices.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
            stored.Should().ContainSingle("el receptor es idempotente por eventoId");
            stored[0].ToJson().Should().NotContain(visit, "el registro de envíos no guarda identificadores de recurso");
        }
        finally
        {
            await mongo.DropDatabaseAsync(name);
        }
    }

    [Fact]
    public async Task Aviso_Con_Datos_Extra_Rol_Cliente_O_Sin_Permiso_Se_Rechaza_Sin_Repetir_El_Texto_CA_064_08()
    {
        var marketKey = RandomNumberGenerator.GetBytes(32);
        await using var host = await SecurityMessagingFactory.CreateAsync(new Dictionary<string, string?>
        {
            ["Security:InternalTokens:MarketKey"] = Convert.ToBase64String(marketKey),
            ["Security:InternalTokens:IdentityKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        });
        using var client = host.CreateClient();
        var visit = ObjectId.GenerateNewId().ToString();
        var extra = "extra-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
        var credential = Spec037InternoAvisosSeguridadTests.Mint("knock-market", marketKey, _type);
        static string Id() => "TecnicoNoSePresento:" + Guid.NewGuid().ToString("N");

        using var withText = await SecureHttp.PostJsonAsync(
            client, _path, new { eventoId = Id(), tipo = _type, destinatarioId = Guid.NewGuid().ToString("D"), rol = "tecnico", recurso = visit, texto = extra }, credential);
        using var clientRole = await SecureHttp.PostJsonAsync(
            client, _path, new { eventoId = Id(), tipo = _type, destinatarioId = Guid.NewGuid().ToString("D"), rol = "cliente", recurso = visit }, credential);
        using var otherPermission = await SecureHttp.PostJsonAsync(
            client, _path, new { eventoId = Id(), tipo = _type, destinatarioId = Guid.NewGuid().ToString("D"), rol = "tecnico", recurso = visit }, Spec037InternoAvisosSeguridadTests.Mint("knock-market", marketKey, "opinion_oculta"));

        withText.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        clientRole.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        otherPermission.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await withText.Content.ReadAsStringAsync()).Should().NotContain(extra);
    }
}
