using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using Xunit;

namespace Knock.IntegrationTest.Identity;

/// <summary>
/// Spec 053, R-25 y CA-39: las rutas del panel responden 429 con <c>Retry-After</c> al pasar el límite de su sesión (120 por minuto en lecturas, 30 por minuto al abrir y decidir) y 413 con un cuerpo mayor
/// al tope de la decisión. El contador es por sesión del panel (<c>sid</c>): otra sesión de la misma cuenta no lo comparte. Host real con MongoDB y Redis; solo datos ficticios.
/// </summary>
public sealed class Spec053LimitesPanelTests
{
    private const int _readLimit = 120;
    private const int _decisionLimit = 30;

    private static Task<IdentityApiFactory> CreateAsync() => IdentityApiFactory.CreateAsync(PanelReportKit.HostSettings);

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string token, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static async Task<HttpStatusCode> LastStatusAsync(HttpClient client, string token, string path, int count)
    {
        var last = HttpStatusCode.OK;
        for (var index = 0; index < count; index++)
        {
            using var response = await GetAsync(client, token, path);
            last = response.StatusCode;
            if (last == HttpStatusCode.TooManyRequests)
            {
                response.Headers.RetryAfter.Should().NotBeNull("el 429 lleva Retry-After");
                (response.Headers.RetryAfter.Delta ?? TimeSpan.Zero).Should().BePositive();
                return last;
            }

            response.StatusCode.Should().Be(HttpStatusCode.OK, $"la petición {index + 1} está dentro del límite");
        }

        return last;
    }

    [Theory]
    [InlineData("/api/v1/admin/verificaciones", "verificador")]
    [InlineData("/api/v1/admin/reportes", "moderador")]
    public async Task Reads_Pass120PerMinutePerSession_And_The121stIs429WithRetryAfter_CA_053_39(string path, string role)
    {
        using var factory = await CreateAsync();
        using var client = factory.CreateClient();
        var sub = await PanelReportKit.SeedPanelAccountAsync(factory.Services, [role]);
        var token = PanelReportKit.Token(sub);

        (await LastStatusAsync(client, token, path, _readLimit)).Should().Be(HttpStatusCode.OK);
        (await LastStatusAsync(client, token, path, 1)).Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task OpeningAVerification_Passes30PerMinutePerSession_And_The31stIs429_CA_053_39()
    {
        using var factory = await CreateAsync();
        using var client = factory.CreateClient();
        var sub = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);
        var token = PanelReportKit.Token(sub);
        var path = $"/api/v1/admin/verificaciones/{ObjectId.GenerateNewId()}";

        for (var index = 0; index < _decisionLimit; index++)
        {
            using var response = await GetAsync(client, token, path);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound, $"la petición {index + 1} llega al servicio");
        }

        using var limited = await GetAsync(client, token, path);
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task Decisions_Pass30PerMinutePerSession_And_AnotherSessionOfTheSameAccountKeepsItsOwnCounter_CA_053_39()
    {
        using var factory = await CreateAsync();
        using var client = factory.CreateClient();
        var sub = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["moderador"]);
        var first = PanelReportKit.Token(sub);
        var second = PanelReportKit.Token(sub);
        var body = new { gravedad = "baja", motivo = "Revisado por el equipo de prueba" };

        for (var index = 0; index < _decisionLimit; index++)
        {
            using var response = await PanelReportKit.DecideAsync(client, first, ObjectId.GenerateNewId().ToString(), body);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound, $"la decisión {index + 1} llega al servicio");
        }

        using var limited = await PanelReportKit.DecideAsync(client, first, ObjectId.GenerateNewId().ToString(), body);
        using var otherSession = await PanelReportKit.DecideAsync(client, second, ObjectId.GenerateNewId().ToString(), body);

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.Should().NotBeNull();
        otherSession.StatusCode.Should().Be(HttpStatusCode.NotFound, "otra sesión del panel cuenta aparte");
    }

    [Theory]
    [InlineData("reportes", "moderador")]
    [InlineData("verificaciones", "verificador")]
    public async Task ADecisionBodyOver2048Bytes_Returns413_CA_053_39(string resource, string role)
    {
        using var factory = await CreateAsync();
        using var client = factory.CreateClient();
        var sub = await PanelReportKit.SeedPanelAccountAsync(factory.Services, [role]);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/v1/admin/{resource}/{ObjectId.GenerateNewId()}/decision", UriKind.Relative))
        {
            Content = JsonContent.Create(new { motivo = new string('a', 3000) }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PanelReportKit.Token(sub));

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }
}
