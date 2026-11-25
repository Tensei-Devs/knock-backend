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
    private const string _route = "/api/v1/interno/cambios-de-oficio";
    private const string _permission = "cambio-de-oficio";

    private static string Token(string permission = _permission) => InternalServiceTokens.Issue("n/a", "n/a", [permission], "knock-market", "knock-identity");

    private static object Body(string eventId, string trade, string action, string by) => new { eventoId = eventId, tipo = "CambioDeOficio", oficioClave = trade, accion = action, porId = by };

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, object body, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_route, UriKind.Relative)) { Content = JsonContent.Create(body) };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

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

        using var first = await PostAsync(client, Body(eventId, trade, "creado", moderator), Token());
        using var second = await PostAsync(client, Body(eventId, trade, "creado", moderator), Token());
        using var edited = await PostAsync(client, Body($"CambioDeOficio:{ObjectId.GenerateNewId()}", trade, "editado", moderator), Token());

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
        var valid = Body($"CambioDeOficio:{ObjectId.GenerateNewId()}", trade, "creado", Guid.NewGuid().ToString("D"));

        using var anonymous = await PostAsync(client, valid, null);
        using var wrongPermission = await PostAsync(client, valid, Token("opinion-moderada"));
        using var extra = await PostAsync(client, new { eventoId = "CambioDeOficio:x", tipo = "CambioDeOficio", oficioClave = trade, accion = "creado", porId = "p1", nombre = "valor de mas" }, Token());
        using var badAction = await PostAsync(client, Body("CambioDeOficio:y", trade, "borrado", "p1"), Token());
        using var badKey = await PostAsync(client, Body("CambioDeOficio:z", "clave con espacios", "creado", "p1"), Token());

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrongPermission.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        new[] { extra, badAction, badKey }.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.UnprocessableEntity);
        (await factory.Database.GetCollection<BsonDocument>("panel_auditoria").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("recursoId", trade))).Should().Be(0);
    }
}
