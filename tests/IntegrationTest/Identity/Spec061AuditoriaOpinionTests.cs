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
/// Spec 061 (R-06 y R-07): <c>POST /api/v1/interno/opiniones-moderadas</c> en Identidad deja <c>opinion_ocultada</c> u <c>opinion_mostrada</c> en <c>panel_auditoria</c>, una sola vez por <c>eventoId</c> y sin
/// ningún texto. MongoDB real; solo datos ficticios.
/// </summary>
public sealed class Spec061AuditoriaOpinionTests
{
    private const string _route = "/api/v1/interno/opiniones-moderadas";
    private const string _permission = "opinion-moderada";

    private static string Token(string permission = _permission) => InternalServiceTokens.Issue("n/a", "n/a", [permission], "knock-market", "knock-identity");

    private static object Body(string eventId, string opinion, string action, string by) =>
        new { eventoId = eventId, tipo = "OpinionModerada", opinionId = opinion, tecnicoId = ObjectId.GenerateNewId().ToString(), accion = action, porId = by };

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
    public async Task Evento_Entregado_Dos_Veces_Deja_Una_Sola_Auditoria_Sin_Texto_CA_061_09()
    {
        await using var factory = await IdentityApiFactory.CreateAsync(new Dictionary<string, string?> { ["Security:InternalTokens:MarketKey"] = InternalServiceTokens.KeyBase64 });
        using var client = factory.CreateClient();
        var opinion = ObjectId.GenerateNewId().ToString();
        var moderator = Guid.NewGuid().ToString("D");
        var eventId = $"OpinionModerada:{opinion}:1";

        using var first = await PostAsync(client, Body(eventId, opinion, "ocultada", moderator), Token());
        using var second = await PostAsync(client, Body(eventId, opinion, "ocultada", moderator), Token());
        using var shown = await PostAsync(client, Body($"OpinionModerada:{opinion}:2", opinion, "mostrada", moderator), Token());

        new[] { first, second, shown }.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Accepted);
        var entries = await factory.Database.GetCollection<BsonDocument>("panel_auditoria").Find(Builders<BsonDocument>.Filter.Eq("recursoId", opinion)).ToListAsync();
        entries.Select(e => e["evento"].AsString).Should().BeEquivalentTo("opinion_ocultada", "opinion_mostrada");
        entries.Should().OnlyContain(e => e["sub"].AsString == moderator && e["permiso"].AsString == "reviews:moderate");
        entries.Select(e => e.ToJson()).Should().NotContain(json => json.Contains("texto") || json.Contains("motivo"));
    }

    [Fact]
    public async Task Sin_Token_Sin_Permiso_O_Con_Campos_De_Mas_Se_Rechaza_CA_061_09()
    {
        await using var factory = await IdentityApiFactory.CreateAsync(new Dictionary<string, string?> { ["Security:InternalTokens:MarketKey"] = InternalServiceTokens.KeyBase64 });
        using var client = factory.CreateClient();
        var opinion = ObjectId.GenerateNewId().ToString();
        var valid = Body($"OpinionModerada:{opinion}:1", opinion, "ocultada", Guid.NewGuid().ToString("D"));

        using var anonymous = await PostAsync(client, valid, null);
        using var wrongPermission = await PostAsync(client, valid, Token("reporte-no-terminado-creado"));
        using var extra = await PostAsync(client, new { eventoId = "OpinionModerada:x:1", tipo = "OpinionModerada", opinionId = opinion, tecnicoId = opinion, accion = "ocultada", porId = "p1", motivo = "texto de mas" }, Token());
        using var badAction = await PostAsync(client, Body("OpinionModerada:y:1", opinion, "borrada", "p1"), Token());

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrongPermission.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        extra.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        badAction.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await factory.Database.GetCollection<BsonDocument>("panel_auditoria").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("recursoId", opinion))).Should().Be(0);
    }
}
