extern alias MarketApi;

using System.Net;
using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Knock.SecurityTest.Support;
using MarketApi::Knock.Market.Api.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.SecurityTest.Market;

/// <summary>
/// Spec 061 (CA-05): con sesión del panel pero sin el permiso de moderar no se vuelve a mostrar una opinión oculta. Mercado real en Production con MongoDB real, la puerta de permisos
/// doblada y una opinión oculta sembrada; el control es la misma petición con el permiso. Solo datos ficticios.
/// </summary>
public sealed class AccesoModeracionDeOpinionesSecurityTests
{
    [Fact]
    public async Task WithoutTheModerationPermission_AHiddenReviewStaysHidden_CA_061_05()
    {
        var name = "market_mod_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        var settings = new Dictionary<string, string?>(PanelReportKit.HostSettings)
        {
            ["Mongo:Databases:Catalog"] = name,
            ["Mongo:Databases:Reviews"] = name,
        };
        using var factory = await SecurityMarketFactory.CreateAsync(settings);
        var gate = new SwitchableGate();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IPanelPermissionGate>(gate)));
        using var client = app.CreateClient();
        await MongoCollections.CreateIfMissingAsync(factory.Database, "opiniones", "tecnicos", "outbox");
        var technician = ObjectId.GenerateNewId();
        await factory.Database.GetCollection<BsonDocument>("tecnicos").InsertOneAsync(new BsonDocument { ["_id"] = technician, ["usuarioId"] = TokenForge.Sub() });
        var review = ObjectId.GenerateNewId();
        var reviews = factory.Database.GetCollection<BsonDocument>("opiniones");
        await reviews.InsertOneAsync(new BsonDocument
        {
            ["_id"] = review,
            ["tecnicoId"] = technician,
            ["visitaId"] = ObjectId.GenerateNewId(),
            ["solicitudId"] = ObjectId.GenerateNewId(),
            ["clienteId"] = TokenForge.Sub(),
            ["calificacion"] = 2,
            ["moderacion"] = "publicada",
            ["oculta"] = true,
            ["ocultaMotivo"] = "Lenguaje ofensivo hacia el cliente",
            ["creadoEn"] = DateTime.UtcNow,
        });
        var byId = Builders<BsonDocument>.Filter.Eq("_id", review);
        var before = (await reviews.Find(byId).SingleAsync()).ToJson();
        var path = $"/api/v1/admin/opiniones/{review}/mostrar";
        var panel = PanelReportKit.Token(TokenForge.Sub());

        using var denied = await SecureHttp.SendAsync(client, HttpMethod.Post, path, bearer: panel);

        await SecureHttp.AssertProblemAsync(denied, HttpStatusCode.Forbidden);
        (await reviews.Find(byId).SingleAsync()).ToJson().Should().Be(before, "una petición rechazada no cambia nada");

        gate.Decision = PanelDecision.Allowed;
        using var allowed = await SecureHttp.SendAsync(client, HttpMethod.Post, path, bearer: panel);

        allowed.StatusCode.Should().Be(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());
        (await reviews.Find(byId).SingleAsync())["oculta"].AsBoolean.Should().BeFalse();
    }

    private sealed class SwitchableGate : IPanelPermissionGate
    {
        public PanelDecision Decision { get; set; } = PanelDecision.Denied;

        public Task<PanelDecision> DecideAsync(string subject, string sessionId, string permissionsVersion, string permission, CancellationToken cancellationToken) =>
            Task.FromResult(permission == PanelAuthentication.ReviewsModeratePermission ? Decision : PanelDecision.Denied);
    }
}
