using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>
/// Spec 053 (BB13): la ruta interna <c>GET /api/v1/interno/visitas/{visitaId}/reporte-no-terminado</c> de Mercado, que solo llama Identidad con su token de servicio, el permiso
/// <c>reporte-no-terminado</c> y el <c>visitaId</c> en el token. MongoDB real; solo datos ficticios.
/// </summary>
public sealed class Spec053ReporteNoTerminadoMarketTests
{
    private const string _permission = "reporte-no-terminado";
    private const string _text = "El trabajo no quedo terminado, ficticio";

    private static readonly Dictionary<string, string?> _settings = new() { ["Security:InternalTokens:IdentityKey"] = InternalServiceTokens.KeyBase64 };

    private static string Token(string visit, string permission = _permission, byte[]? key = null) =>
        InternalServiceTokens.Issue("n/a", "n/a", [permission], key: key, extra: new Dictionary<string, string> { ["visitaId"] = visit });

    private static async Task<HttpResponseMessage> GetAsync(Spec020Host host, string visit, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/api/v1/interno/visitas/{visit}/reporte-no-terminado", UriKind.Relative));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await host.Client.SendAsync(request);
    }

    private static async Task<(ObjectId Id, string Client, string Technician)> SeedVisitAsync(Spec020Host host, bool withReport)
    {
        var document = new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "clienteId", Guid.NewGuid().ToString("D") },
            { "tecnicoUsuarioId", Guid.NewGuid().ToString("D") },
            { "estado", "terminada" },
        };
        if (withReport)
        {
            document.Add("reporteNoTerminado", new BsonDocument { { "texto", _text }, { "estado", "en_revision" }, { "en", new BsonDateTime(DateTime.UtcNow) } });
        }

        await host.Database.GetCollection<BsonDocument>("visitas").InsertOneAsync(document);
        return (document["_id"].AsObjectId, document["clienteId"].AsString, document["tecnicoUsuarioId"].AsString);
    }

    [Fact]
    public async Task Get_WithTheBoundToken_ReturnsTheReportTextAndIds_CA_053_REPORTE_NO_TERMINADO()
    {
        await using var host = await Spec020Host.StartAsync(_settings);
        var (id, client, technician) = await SeedVisitAsync(host, withReport: true);

        using var response = await GetAsync(host, id.ToString(), Token(id.ToString()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(id.ToString()).And.Contain(client).And.Contain(technician).And.Contain(_text);
    }

    [Fact]
    public async Task Get_WithoutReportOrWithAnInvalidId_Answers404_CA_053_REPORTE_NO_TERMINADO()
    {
        await using var host = await Spec020Host.StartAsync(_settings);
        var (withoutReport, _, _) = await SeedVisitAsync(host, withReport: false);

        using var none = await GetAsync(host, withoutReport.ToString(), Token(withoutReport.ToString()));
        var missing = ObjectId.GenerateNewId().ToString();
        using var unknown = await GetAsync(host, missing, Token(missing));
        using var invalid = await GetAsync(host, "no-es-un-id", Token("no-es-un-id"));

        none.StatusCode.Should().Be(HttpStatusCode.NotFound);
        invalid.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await none.Content.ReadAsStringAsync()).Should().NotContain(_text);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_WithoutTokenWithAnotherPermissionOrAnotherVisit_NeverReturnsTheText_CA_053_REPORTE_NO_TERMINADO()
    {
        await using var host = await Spec020Host.StartAsync(_settings);
        var (id, _, _) = await SeedVisitAsync(host, withReport: true);
        var other = ObjectId.GenerateNewId().ToString();

        using var anonymous = await GetAsync(host, id.ToString(), null);
        using var historyOnly = await GetAsync(host, id.ToString(), Token(id.ToString(), "historial"));
        using var blocksOnly = await GetAsync(host, id.ToString(), Token(id.ToString(), "bloqueos"));
        using var wrongKey = await GetAsync(host, id.ToString(), Token(id.ToString(), key: new byte[32]));
        using var anotherVisit = await GetAsync(host, id.ToString(), Token(other));

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrongKey.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        historyOnly.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        blocksOnly.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        anotherVisit.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        foreach (var response in new[] { anonymous, historyOnly, blocksOnly, wrongKey, anotherVisit })
        {
            (await response.Content.ReadAsStringAsync()).Should().NotContain(_text);
        }
    }

    [Fact]
    public async Task OtherInternalPermissions_DoNotOpenTheReportRoute_AndTheReportPermissionOpensNoOtherRoute_CA_053_REPORTE_NO_TERMINADO()
    {
        await using var host = await Spec020Host.StartAsync(_settings);
        var token = Token(ObjectId.GenerateNewId().ToString());
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/interno/bloqueos", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
