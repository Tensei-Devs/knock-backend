using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Identity;

/// <summary>
/// Spec 045 (R-20, CA-35): <c>POST /api/v1/interno/cambios-de-oficio</c> en Identidad deja <c>oficio_creado</c> u <c>oficio_editado</c> en <c>panel_auditoria</c> con la persona del panel, el permiso
/// <c>catalog:manage</c> y la clave del oficio, una sola vez por <c>eventoId</c> y sin ningún valor del oficio. MongoDB real; solo datos ficticios.
/// </summary>
public sealed class Spec045AuditoriaOficioTests
{
    private const string _path = "/api/v1/interno/cambios-de-oficio";
    private const string _grant = "cambio-de-oficio";

    private static string ServiceToken(string permission = _grant) =>
        InternalServiceTokens.Issue("n/a", "n/a", [permission], "knock-market", "knock-identity");

    private static object ChangeBody(string eventId, string trade, string action, string by) =>
        new { eventoId = eventId, tipo = "CambioDeOficio", oficioClave = trade, accion = action, porId = by };

    private static async Task<HttpResponseMessage> Deliver(HttpClient client, object body, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_path, UriKind.Relative)) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = token is null ? null : new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Evento_Entregado_Dos_Veces_Deja_Una_Sola_Auditoria_Con_Quien_Y_Cual_CA_045_35()
    {
        await using var factory = await IdentityApiFactory.CreateAsync(new Dictionary<string, string?> { ["Security:InternalTokens:MarketKey"] = InternalServiceTokens.KeyBase64 });
        using var client = factory.CreateClient();
        var trade = $"t{System.Security.Cryptography.RandomNumberGenerator.GetInt32(1000, 9999)}";
        var moderator = Guid.NewGuid().ToString("D");
        var eventId = $"CambioDeOficio:{ObjectId.GenerateNewId()}";

        using var first = await Deliver(client, ChangeBody(eventId, trade, "creado", moderator), ServiceToken());
        using var second = await Deliver(client, ChangeBody(eventId, trade, "creado", moderator), ServiceToken());
        using var edited = await Deliver(client, ChangeBody($"CambioDeOficio:{ObjectId.GenerateNewId()}", trade, "editado", moderator), ServiceToken());

        new[] { first, second, edited }.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Accepted);
        var entries = await factory.Database.GetCollection<BsonDocument>("panel_auditoria").Find(Builders<BsonDocument>.Filter.Eq("recursoId", trade)).ToListAsync();
        entries.Select(e => e["evento"].AsString).Should().BeEquivalentTo("oficio_creado", "oficio_editado");
        entries.Should().OnlyContain(e => e["sub"].AsString == moderator && e["permiso"].AsString == "catalog:manage" && e["resultado"].AsString == "permitido");
    }

    [Fact]
    public async Task Sin_Token_Sin_Permiso_O_Con_Campos_De_Mas_Se_Rechaza_CA_045_35()
    {
        await using var factory = await IdentityApiFactory.CreateAsync(new Dictionary<string, string?> { ["Security:InternalTokens:MarketKey"] = InternalServiceTokens.KeyBase64 });
        using var client = factory.CreateClient();
        var trade = $"t{System.Security.Cryptography.RandomNumberGenerator.GetInt32(1000, 9999)}";
        var valid = ChangeBody($"CambioDeOficio:{ObjectId.GenerateNewId()}", trade, "creado", Guid.NewGuid().ToString("D"));

        using var anonymous = await Deliver(client, valid, null);
        using var wrongPermission = await Deliver(client, valid, ServiceToken("opinion-moderada"));
        using var extra = await Deliver(client, new { eventoId = "CambioDeOficio:x", tipo = "CambioDeOficio", oficioClave = trade, accion = "creado", porId = "p1", nombre = "valor de mas" }, ServiceToken());
        using var badAction = await Deliver(client, ChangeBody("CambioDeOficio:y", trade, "borrado", "p1"), ServiceToken());
        using var badKey = await Deliver(client, ChangeBody("CambioDeOficio:z", "clave con espacios", "creado", "p1"), ServiceToken());

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrongPermission.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        new[] { extra, badAction, badKey }.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.UnprocessableEntity);
        (await factory.Database.GetCollection<BsonDocument>("panel_auditoria").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("recursoId", trade))).Should().Be(0);
    }
}
