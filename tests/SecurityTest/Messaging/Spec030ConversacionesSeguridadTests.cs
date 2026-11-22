using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Knock.SecurityTest.Support;
using Xunit;

namespace Knock.SecurityTest.Messaging;

/// <summary>
/// Spec 030, T-05 y T-07 (KNOCK-73): seguridad negativa de las rutas de conversaciones: sin sesión, con sesión alterada, identificadores y cursores mal formados, formato de los cuerpos y
/// que ningún error repita lo enviado. No necesitan MongoDB: cada rechazo ocurre antes del acceso a datos. Solo datos ficticios.
/// </summary>
public sealed class Spec030ConversacionesSeguridadTests
{
    private const string _validId = "507f1f77bcf86cd799439011";
    private const string _echo = "valor-ajeno-de-prueba-7711234567";

    public static TheoryData<string, string> Routes() => new()
    {
        { "POST", "/api/v1/conversaciones" },
        { "GET", "/api/v1/conversaciones" },
        { "GET", "/api/v1/conversaciones/no-leidas" },
        { "GET", $"/api/v1/conversaciones/{_validId}" },
        { "GET", $"/api/v1/conversaciones/{_validId}/mensajes" },
        { "POST", $"/api/v1/conversaciones/{_validId}/leido" },
    };

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task WithoutAToken_EveryRouteReturns401_CA_030_20(string method, string path)
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();

        using var response = await SecureHttp.SendAsync(client, new HttpMethod(method), path);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("code").GetString().Should().Be("unauthorized");
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task WithAnExpiredOrForgedToken_EveryRouteReturns401_CA_030_20(string method, string path)
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();

        foreach (var token in new[] { TokenForge.Expired(), TokenForge.WrongIssuer(), TokenForge.WrongAudience() })
        {
            using var response = await SecureHttp.SendAsync(client, new HttpMethod(method), path, bearer: token);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }

    [Theory]
    [InlineData("GET", "/api/v1/conversaciones/{0}")]
    [InlineData("GET", "/api/v1/conversaciones/{0}/mensajes")]
    [InlineData("POST", "/api/v1/conversaciones/{0}/leido")]
    public async Task AMalformedId_Returns400_AndNeverRepeatsIt_CA_030_03(string method, string template)
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();
        var token = TokenForge.Valid();

        foreach (var id in new[] { _echo, "123", "507f1f77bcf86cd79943901z", "507f1f77bcf86cd7994390111" })
        {
            using var response = method == "POST"
                ? await SecureHttp.PostJsonAsync(client, string.Format(System.Globalization.CultureInfo.InvariantCulture, template, id), new { hasta = DateTime.UtcNow }, token)
                : await SecureHttp.SendAsync(client, HttpMethod.Get, string.Format(System.Globalization.CultureInfo.InvariantCulture, template, id), bearer: token);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, id);
            var body = await response.Content.ReadAsStringAsync();
            ProblemBody.Reasons(body).Should().NotContain(id);
            using var problem = JsonDocument.Parse(body);
            problem.RootElement.GetProperty("code").GetString().Should().Be("invalid_id");
        }
    }

    [Theory]
    [InlineData("/api/v1/conversaciones")]
    [InlineData("/api/v1/conversaciones/507f1f77bcf86cd799439011/mensajes")]
    public async Task AMalformedCursor_Returns400_AndNeverRepeatsIt_CA_030_16(string path)
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();

        using var response = await SecureHttp.SendAsync(client, HttpMethod.Get, $"{path}?cursor={Uri.EscapeDataString(_echo)}", bearer: TokenForge.Valid());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        ProblemBody.Reasons(body).Should().NotContain(_echo);
        using var problem = JsonDocument.Parse(body);
        problem.RootElement.GetProperty("code").GetString().Should().Be("invalid_cursor");
    }

    [Theory]
    [InlineData("/api/v1/conversaciones", "0")]
    [InlineData("/api/v1/conversaciones", "21")]
    [InlineData("/api/v1/conversaciones", "-3")]
    [InlineData("/api/v1/conversaciones/507f1f77bcf86cd799439011/mensajes", "0")]
    [InlineData("/api/v1/conversaciones/507f1f77bcf86cd799439011/mensajes", "51")]
    public async Task ALimitOutOfRange_Returns422WithErrorsLimit_CA_030_11(string path, string limit)
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();

        using var response = await SecureHttp.SendAsync(client, HttpMethod.Get, $"{path}?limit={limit}", bearer: TokenForge.Valid());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("code").GetString().Should().Be("validation_failed");
        problem.RootElement.GetProperty("errors").TryGetProperty("limit", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ACreateBodyWithBadIds_Returns422PerField_AndNeverEchoesTheValue_CA_030_15()
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();
        var token = TokenForge.Valid();

        using var bad = await SecureHttp.PostJsonAsync(client, "/api/v1/conversaciones", new { solicitudId = _echo, tecnicoId = _echo }, token);
        using var missing = await SecureHttp.PostJsonAsync(client, "/api/v1/conversaciones", new { tecnicoId = _validId }, token);

        bad.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var badBody = await bad.Content.ReadAsStringAsync();
        ProblemBody.Reasons(badBody).Should().NotContain(_echo).And.NotContain("7711234567");
        using var badProblem = JsonDocument.Parse(badBody);
        badProblem.RootElement.GetProperty("errors").TryGetProperty("solicitudId", out _).Should().BeTrue();
        badProblem.RootElement.GetProperty("errors").TryGetProperty("tecnicoId", out _).Should().BeTrue();
        missing.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var missingProblem = JsonDocument.Parse(await missing.Content.ReadAsStringAsync());
        missingProblem.RootElement.GetProperty("errors").TryGetProperty("solicitudId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task AReadBodyWithoutUntil_Returns422_AndAnUnreadableBodyReturns400_CA_030_12()
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();
        var token = TokenForge.Valid();
        var path = $"/api/v1/conversaciones/{_validId}/leido";

        using var empty = await SecureHttp.PostJsonAsync(client, path, new { }, token);
        using var unreadable = await SecureHttp.SendRawJsonAsync(client, HttpMethod.Post, path, "{\"hasta\":\"no-es-una-fecha\"", token);
        using var wrongType = await SecureHttp.SendRawJsonAsync(client, HttpMethod.Post, path, $"{{\"hasta\":\"{_echo}\"}}", token);

        empty.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var problem = JsonDocument.Parse(await empty.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("errors").TryGetProperty("hasta", out _).Should().BeTrue();
        unreadable.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        wrongType.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ProblemBody.Reasons(await wrongType.Content.ReadAsStringAsync()).Should().NotContain(_echo);
    }

    [Fact]
    public async Task APermissionDeniedByMarket_Returns404_WithTheUniformBody_CA_030_03()
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();

        using var response = await SecureHttp.PostJsonAsync(client, "/api/v1/conversaciones", new { solicitudId = _validId, tecnicoId = _validId }, TokenForge.Valid());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("code").GetString().Should().Be("not_found");
        SecureHttp.LeakyHeaders.Where(response.Headers.Contains).Should().BeEmpty();
    }
}
