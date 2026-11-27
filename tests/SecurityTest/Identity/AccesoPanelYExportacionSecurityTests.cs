extern alias IdentityApi;

using System.Net;
using AwesomeAssertions;
using IdentityApi::Knock.Identity.Api.Modules.Moderation.Dtos;
using IdentityApi::Knock.Identity.Api.Modules.Moderation.Services;
using IdentityApi::Knock.Identity.Api.Modules.Panel.Data;
using Knock.IntegrationTest.Support;
using Knock.SecurityTest.Support;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.SecurityTest.Identity;

/// <summary>
/// Specs 053 y 039: abrir y decidir una verificación sembrada exige la sesión del panel con el rol de verificación, y cada persona solo ve su propia exportación. Identidad real en Production con
/// MongoDB y Redis reales. Solo datos ficticios.
/// </summary>
public sealed class AccesoPanelYExportacionSecurityTests
{
    private const string _verifications = "/api/v1/admin/verificaciones/";
    private const string _exports = "/api/v1/me/exportaciones";

    private static readonly string[] _reviewEvents = ["verificacion_abierta", "verificacion_decidida"];

    [Fact]
    public async Task OnlyTheVerifier_OpensAndDecidesAnotherPersonsVerification_CA_053_03_CA_053_08()
    {
        await using var factory = await SecurityIdentityFactory.CreateAsync(PanelReportKit.HostSettings);
        await using var app = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IVerificationPhotoLinks>(new FakePhotoLinks())));
        using var client = app.CreateClient();
        await app.Services.GetRequiredService<PanelAuditoriaStore>().EnsureIndexesAsync(CancellationToken.None);
        var owner = TokenForge.Sub();
        var id = await SeedVerificationAsync(factory, owner);
        var verifications = factory.Database.GetCollection<BsonDocument>("verificaciones");
        var audit = factory.Database.GetCollection<BsonDocument>("panel_auditoria");
        var byResource = Builders<BsonDocument>.Filter.Eq("recursoId", id.ToString());
        var before = (await verifications.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).SingleAsync()).ToJson();
        var moderator = await PanelReportKit.SeedPanelAccountAsync(app.Services, ["moderador"]);
        (string? Token, HttpStatusCode Status)[] intruders =
        [
            (null, HttpStatusCode.Unauthorized),
            (TokenForge.Valid(owner), HttpStatusCode.Unauthorized),
            (TokenForge.Valid(), HttpStatusCode.Unauthorized),
            (PanelReportKit.Token(moderator), HttpStatusCode.Forbidden),
        ];

        foreach (var (token, status) in intruders)
        {
            using var open = await SecureHttp.SendAsync(client, HttpMethod.Get, _verifications + id, bearer: token);
            using var decide = await SecureHttp.PostJsonAsync(client, _verifications + id + "/decision", new { decision = "aprobada" }, token);

            await SecureHttp.AssertProblemAsync(open, status, owner, FakePhotoLinks.Host);
            await SecureHttp.AssertProblemAsync(decide, status, owner);
        }

        (await verifications.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).SingleAsync()).ToJson().Should().Be(before, "una petición rechazada no cambia nada");
        var opened = byResource & Builders<BsonDocument>.Filter.In("evento", _reviewEvents);
        (await audit.CountDocumentsAsync(opened)).Should().Be(0, "nadie abrió ni decidió");

        var verifier = PanelReportKit.Token(await PanelReportKit.SeedPanelAccountAsync(app.Services, ["verificador"]));
        using var allowedOpen = await SecureHttp.SendAsync(client, HttpMethod.Get, _verifications + id, bearer: verifier);
        using var allowedDecision = await PanelReportKit.SendAsync(client, HttpMethod.Post, _verifications + id + "/decision", verifier, new { decision = "aprobada" });

        allowedOpen.StatusCode.Should().Be(HttpStatusCode.OK, "el verificador sí abre");
        (await allowedOpen.Content.ReadAsStringAsync()).Should().Contain(FakePhotoLinks.Host);
        allowedDecision.StatusCode.Should().Be(HttpStatusCode.OK, "el verificador sí decide");
        (await verifications.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).SingleAsync())["estado"].AsString.Should().Be("verificada");
    }

    [Fact]
    public async Task AnotherPerson_NeitherSeesNorChangesMyExport_CA_039_35()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var owner = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        var other = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        using var requested = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Post, _exports, bearer: owner.AccessToken);
        requested.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var byOwner = Builders<BsonDocument>.Filter.Eq("sub", owner.Sub);
        var before = (await hosts.Identity.Users.Find(byOwner).SingleAsync()).ToJson();

        using var foreignRead = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Get, _exports + "/ultima", bearer: other.AccessToken);
        // El cuerpo con el identificador ajeno no cuenta: la exportación se pide para quien lleva el token.
        using var foreignRequest = await SecureHttp.PostJsonAsync(hosts.IdentityClient, _exports, new { sub = owner.Sub, usuarioId = owner.Sub }, other.AccessToken);

        var problem = await SecureHttp.AssertProblemAsync(foreignRead, HttpStatusCode.NotFound, owner.Sub);
        problem.Should().Contain("export_not_found");
        foreignRequest.StatusCode.Should().Be(HttpStatusCode.Accepted, "pide la suya: la de otra persona en curso no la bloquea");
        (await hosts.Identity.Users.Find(byOwner).SingleAsync()).ToJson().Should().Be(before, "la cuenta ajena no cambia");

        using var ownRead = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Get, _exports + "/ultima", bearer: owner.AccessToken);

        ownRead.StatusCode.Should().Be(HttpStatusCode.OK, "su dueño sí la ve");
        (await ownRead.Content.ReadAsStringAsync()).Should().Contain("en_preparacion");
    }

    private static async Task<ObjectId> SeedVerificationAsync(SecurityIdentityFactory factory, string owner)
    {
        var id = ObjectId.GenerateNewId();
        var photos = new BsonArray
        {
            new BsonDocument { ["tipo"] = "ine_frente", ["estado"] = "cifrada", ["blob"] = $"f/{id}/ine_frente" },
        };
        await factory.Database.GetCollection<BsonDocument>("verificaciones").InsertOneAsync(new BsonDocument
        {
            ["_id"] = id,
            ["usuarioId"] = owner,
            ["nivel"] = "basica",
            ["estado"] = "en_revision",
            ["curpHmac"] = id.ToString(),
            ["curpVigente"] = true,
            ["llaveEnvuelta"] = "llave-ficticia",
            ["llaveKekVersion"] = "test-kek",
            ["fotos"] = photos,
            ["enviadoEn"] = DateTime.UtcNow.AddHours(-1),
            ["creadoEn"] = DateTime.UtcNow.AddDays(-1),
        });
        return id;
    }

    private sealed class FakePhotoLinks : IVerificationPhotoLinks
    {
        public const string Host = "almacen.invalid";

        public Task<IReadOnlyList<VerificationPhotoLink>> IssueAsync(string verificationId, IReadOnlyList<string> photoTypes, TimeSpan lifetime, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<VerificationPhotoLink>>(
                [.. photoTypes.Select(type => new VerificationPhotoLink(type, $"https://{Host}/{verificationId}/{type}", DateTime.UtcNow + lifetime))]);
    }
}
