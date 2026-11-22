using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Requests;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Tecnicos;

/// <summary>
/// Spec 053, R-20 (T-06): la ruta interna <c>POST /api/v1/interno/tecnicos-ocultados</c> de Mercado, que solo llama Identidad con su token de servicio. MongoDB real; solo datos ficticios.
/// </summary>
public sealed class Spec053TecnicoOcultadoTests
{
    private const string _route = "/api/v1/interno/tecnicos-ocultados";
    private const string _permission = "tecnico-ocultado";

    private static readonly Dictionary<string, string?> _settings = new() { ["Security:InternalTokens:IdentityKey"] = InternalServiceTokens.KeyBase64 };

    private static async Task<HttpResponseMessage> PostAsync(Spec020Host host, object body, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_route, UriKind.Relative)) { Content = JsonContent.Create(body) };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await host.Client.SendAsync(request);
    }

    private static string Token(params string[] permissions) => InternalServiceTokens.Issue("identity", "n/a", permissions);

    private static async Task<string> SeedTechnicianAsync(Spec020Host host)
    {
        var sub = Guid.NewGuid().ToString("D");
        await host.Database.GetCollection<BsonDocument>("tecnicos").InsertOneAsync(
            new BsonDocument { { "_id", ObjectId.GenerateNewId() }, { "usuarioId", sub }, { "nombre", "Técnica Ana de prueba" }, { "estado", "activo" }, { "oculto", false } });
        return sub;
    }

    private static async Task<BsonDocument> ReadAsync(Spec020Host host, string sub) =>
        await host.Database.GetCollection<BsonDocument>("tecnicos").Find(Builders<BsonDocument>.Filter.Eq("usuarioId", sub)).FirstAsync();

    [Fact]
    public async Task HidesTheTechnician_AndRecordsTheReport_CA_053_34()
    {
        await using var host = await Spec020Host.StartAsync(_settings);
        var sub = await SeedTechnicianAsync(host);
        var report = ObjectId.GenerateNewId().ToString();

        using var response = await PostAsync(host, new { eventoId = $"TecnicoOcultado:{report}:1", usuarioId = sub, reporteId = report }, Token(_permission));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var document = await ReadAsync(host, sub);
        document["oculto"].AsBoolean.Should().BeTrue();
        document["ocultoPorReporteId"].AsString.Should().Be(report);
        document.Contains("ocultoHasta").Should().BeFalse();
    }

    [Fact]
    public async Task DeliveringTheSameEventTwice_ChangesNothingTheSecondTime_CA_053_34()
    {
        await using var host = await Spec020Host.StartAsync(_settings);
        var sub = await SeedTechnicianAsync(host);
        var report = ObjectId.GenerateNewId().ToString();
        var body = new { eventoId = $"TecnicoOcultado:{report}:1", usuarioId = sub, reporteId = report };

        using var first = await PostAsync(host, body, Token(_permission));
        var hiddenAt = (await ReadAsync(host, sub))["ocultoEn"];
        await Task.Delay(TimeSpan.FromMilliseconds(30));
        using var second = await PostAsync(host, body, Token(_permission));

        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        second.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await ReadAsync(host, sub))["ocultoEn"].Should().Be(hiddenAt);
    }

    [Fact]
    public async Task AnUnknownTechnician_Answers202_SoTheOutboxDoesNotRetryForever_CA_053_34()
    {
        await using var host = await Spec020Host.StartAsync(_settings);

        using var response = await PostAsync(host, new { eventoId = "TecnicoOcultado:r:1", usuarioId = Guid.NewGuid().ToString("D"), reporteId = "r" }, Token(_permission));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task WithoutTokenOrWithAnotherPermission_NothingChanges_CA_053_37()
    {
        await using var host = await Spec020Host.StartAsync(_settings);
        var sub = await SeedTechnicianAsync(host);
        var body = new { eventoId = "TecnicoOcultado:r:1", usuarioId = sub, reporteId = "r" };

        using var anonymous = await PostAsync(host, body, null);
        using var historyOnly = await PostAsync(host, body, Token("historial"));
        using var wrongKey = await PostAsync(host, body, InternalServiceTokens.Issue("identity", "n/a", [_permission], key: new byte[32]));

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        historyOnly.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        wrongKey.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ReadAsync(host, sub))["oculto"].AsBoolean.Should().BeFalse();
    }

    [Fact]
    public async Task AMalformedBody_Answers422_WithoutEchoingTheValues_CA_053_37()
    {
        await using var host = await Spec020Host.StartAsync(_settings);

        using var response = await PostAsync(host, new { eventoId = "x", usuarioId = "con espacio", reporteId = "r" }, Token(_permission));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("con espacio");
    }
}
