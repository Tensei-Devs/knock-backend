using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Knock.SecurityTest.Support;
using Xunit;

namespace Knock.SecurityTest.Identity;

/// <summary>
/// Spec 053: seguridad negativa de las rutas del panel de revisión (<c>/api/v1/admin/verificaciones</c> y <c>/api/v1/admin/reportes</c>) con el host de Identidad en Production, MongoDB y Redis reales:
/// 401 sin sesión del panel, 403 sin el permiso, 400 con cuerpo ilegible, 422 con filtros inválidos sin repetir el valor, 413 con un cuerpo excesivo y respuestas que nunca se guardan en caché.
/// Solo datos ficticios.
/// </summary>
public sealed class Spec053SeguridadTests
{
    private const string _verifications = "/api/v1/admin/verificaciones";
    private const string _reports = "/api/v1/admin/reportes";
    private const string _id = "507f1f77bcf86cd799439011";

    private static Task<SecurityIdentityFactory> HostAsync() => SecurityIdentityFactory.CreateAsync(PanelReportKit.HostSettings);

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? token, string? json = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request);
    }

    [Theory]
    [InlineData("GET", _verifications)]
    [InlineData("GET", _verifications + "/" + _id)]
    [InlineData("POST", _verifications + "/" + _id + "/decision")]
    [InlineData("GET", _reports)]
    [InlineData("POST", _reports + "/" + _id + "/decision")]
    public async Task WithoutAPanelSession_Returns401_CA_053_02_CA_053_07_CA_053_13_CA_053_20_CA_053_25(string method, string path)
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var app = TokenForge.Valid();

        using var anonymous = await SendAsync(client, new HttpMethod(method), path, null, method == "POST" ? "{}" : null);
        using var appToken = await SendAsync(client, new HttpMethod(method), path, app, method == "POST" ? "{}" : null);

        await SecureHttp.AssertProblemAsync(anonymous, HttpStatusCode.Unauthorized);
        await SecureHttp.AssertProblemAsync(appToken, HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("GET", _verifications, "moderador")]
    [InlineData("GET", _verifications + "/" + _id, "moderador")]
    [InlineData("POST", _verifications + "/" + _id + "/decision", "moderador")]
    [InlineData("GET", _reports, "verificador")]
    [InlineData("POST", _reports + "/" + _id + "/decision", "verificador")]
    public async Task WithoutThePermissionOfTheRoute_Returns403_CA_053_03_CA_053_08(string method, string path, string role)
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var person = await PanelReportKit.SeedPanelAccountAsync(factory.Services, [role]);

        using var response = await SendAsync(client, new HttpMethod(method), path, PanelReportKit.Token(person), method == "POST" ? """{"decision":"aprobar","motivo":"texto"}""" : null);

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TheVerifierSeesTheQueueWithNoCache_CA_053_01_CA_053_35()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);

        using var response = await SendAsync(client, HttpMethod.Get, _verifications, PanelReportKit.Token(verifier));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue("las rutas del panel nunca se guardan en caché");
        (await response.Content.ReadAsStringAsync()).Should().Contain("items");
    }

    [Fact]
    public async Task AnUnreadableBody_Returns400AndSavesNothing_CA_053_16()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);

        using var response = await SendAsync(client, HttpMethod.Post, _verifications + "/" + _id + "/decision", PanelReportKit.Token(verifier), "{no es json");

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.BadRequest, "no es json");
    }

    [Theory]
    [InlineData(_reports + "?limite=51", "51")]
    [InlineData(_reports + "?gravedad=valor-raro-xyz", "valor-raro-xyz")]
    [InlineData(_reports + "?estado=valor-raro-xyz", "valor-raro-xyz")]
    public async Task InvalidFiltersOnTheReportQueue_Return422WithoutRepeatingTheValue_CA_053_22_CA_053_23_CA_053_33_CA_053_38(string path, string value)
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var moderator = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["moderador"]);

        using var response = await SendAsync(client, HttpMethod.Get, path, PanelReportKit.Token(moderator));

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, value);
    }

    [Theory]
    [InlineData(_verifications + "?tipo=valor-raro-xyz", "valor-raro-xyz")]
    [InlineData(_verifications + "?estado=valor-raro-xyz", "valor-raro-xyz")]
    [InlineData(_verifications + "?limite=51", "51")]
    public async Task InvalidFiltersOnTheVerificationQueue_Return422WithoutRepeatingTheValue_CA_053_30_CA_053_38(string path, string value)
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);

        using var response = await SendAsync(client, HttpMethod.Get, path, PanelReportKit.Token(verifier));

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, value);
    }

    [Theory]
    [InlineData(_verifications + "/" + _id + "/decision", "verificador")]
    [InlineData(_reports + "/" + _id + "/decision", "moderador")]
    public async Task AnOversizedDecisionBody_Returns413(string path, string role)
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var person = await PanelReportKit.SeedPanelAccountAsync(factory.Services, [role]);
        var json = $$"""{"motivo":"{{new string('a', 20000)}}"}""";

        using var response = await SendAsync(client, HttpMethod.Post, path, PanelReportKit.Token(person), json);

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.RequestEntityTooLarge);
    }
}
