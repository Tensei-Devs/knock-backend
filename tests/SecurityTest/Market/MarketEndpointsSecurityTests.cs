using System.Net;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Knock.SecurityTest.Support;
using Xunit;

namespace Knock.SecurityTest.Market;

/// <summary>TT-PRU-01 (<c>GET /api/v1/oficios</c>, público) y TT-API-07 (<c>GET /api/v1/colonias/en-punto</c>, con sesión). Host real en Production con MongoDB real.</summary>
public class MarketEndpointsSecurityTests
{
    private const string _oficios = "/api/v1/oficios";
    private const string _point = "/api/v1/colonias/en-punto";

    private static readonly string[] _oficioFields = ["clave", "modalidad", "nombre"];

    [Fact]
    public async Task Oficios_IsPublic_EvenWithABrokenToken_AndReturnsOnlyActiveOficiosWithThreeFields_SEC_TT_PRU_01_01()
    {
        var hosts = await SecurityHosts.SharedAsync();

        using var anonymous = await SecureHttp.SendAsync(hosts.MarketClient, HttpMethod.Get, _oficios);
        using var broken = await SecureHttp.SendAsync(hosts.MarketClient, HttpMethod.Get, _oficios, bearer: TokenForge.AlgNone());

        foreach (var response in new[] { anonymous, broken })
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var items = (await SecureHttp.ReadJsonAsync(response)).EnumerateArray().ToList();
            items.Should().NotBeEmpty();
            items.Should().OnlyContain(i => i.EnumerateObject().Select(p => p.Name).OrderBy(n => n).SequenceEqual(_oficioFields));
            items.Select(i => i.GetProperty("clave").GetString()).Should().NotContain("OLD");
            response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        }
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task Oficios_RejectsWrites_With405_SEC_TT_PRU_01_02(string method)
    {
        var hosts = await SecurityHosts.SharedAsync();

        using var response = await SecureHttp.SendAsync(hosts.MarketClient, new HttpMethod(method), _oficios, bearer: TokenForge.Valid());

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Oficios_IgnoresQueryStringFilters_SEC_TT_PRU_01_03()
    {
        var hosts = await SecurityHosts.SharedAsync();
        using var plain = await SecureHttp.SendAsync(hosts.MarketClient, HttpMethod.Get, _oficios);
        using var hostile = await SecureHttp.SendAsync(hosts.MarketClient, HttpMethod.Get, _oficios + "?activo=false&clave[$ne]=x&$where=1&nombre=%27%20OR%201=1");

        hostile.StatusCode.Should().Be(HttpStatusCode.OK);
        (await hostile.Content.ReadAsStringAsync()).Should().Be(await plain.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnknownRoutesAndTraversal_Return404ProblemDetailsWithoutDetails_SEC_TT_PRU_01_04()
    {
        var hosts = await SecurityHosts.SharedAsync();

        foreach (var path in new[] { "/api/v1/oficios/..%2f..%2fappsettings.json", "/api/v1/admin", "/appsettings.json" })
        {
            using var response = await SecureHttp.SendAsync(hosts.MarketClient, HttpMethod.Get, path);

            ((int)response.StatusCode).Should().BeOneOf(400, 404);
            (await response.Content.ReadAsStringAsync()).Should().NotContain("stackTrace").And.NotContain("Jwt").And.NotContain("mongodb");
        }
    }

    [Fact]
    public async Task Oficios_GlobalLimitReturns429WithRetryAfter_SEC_TT_PRU_01_05()
    {
        using var factory = await SecurityMarketFactory.CreateAsync(new Dictionary<string, string?>
        {
            ["RateLimiting:Global:PermitLimit"] = "3",
            ["RateLimiting:Global:Window"] = "00:01:00",
        });
        using var client = factory.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            using var ok = await SecureHttp.SendAsync(client, HttpMethod.Get, _oficios, remoteIp: "203.0.113.80");
            ok.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var limited = await SecureHttp.SendAsync(client, HttpMethod.Get, _oficios, remoteIp: "203.0.113.80");
        using var other = await SecureHttp.SendAsync(client, HttpMethod.Get, _oficios, remoteIp: "203.0.113.81");

        await SecureHttp.AssertProblemAsync(limited, HttpStatusCode.TooManyRequests, "203.0.113.80");
        int.Parse(limited.Headers.GetValues("Retry-After").Single(), System.Globalization.CultureInfo.InvariantCulture).Should().BeInRange(1, 60);
        other.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("lat=1 OR 1=1&lon=-99.13")]
    [InlineData("lat={\"$gt\":0}&lon=-99.13")]
    [InlineData("lat=19.43;DROP&lon=-99.13")]
    [InlineData("lat=<script>&lon=-99.13")]
    [InlineData("lat=19.43%0A&lon=-99.13")]
    [InlineData("lat=19.43%0d%0aX-Injected:1&lon=-99.13")]
    [InlineData("lat=%D9%A1%D9%A9.%D9%A4%D9%A3&lon=-99.13")]
    [InlineData("lat=19.43&lon=1e1")]
    [InlineData("lat=NaN&lon=-99.13")]
    [InlineData("lat=Infinity&lon=-99.13")]
    [InlineData("lat=19,43&lon=-99.13")]
    [InlineData("lat=+19.43&lon=-99.13")]
    [InlineData("lat=90.0000001&lon=-99.13")]
    [InlineData("lat=19.43&lon=-180.5")]
    [InlineData("lat=19.43&lat=19.44&lon=-99.13")]
    [InlineData("lat=19.43&lon=-99.13&lon=-99.14")]
    [InlineData("lat[$ne]=1&lon=-99.13")]
    [InlineData("lat=19.43")]
    [InlineData("lon=-99.13")]
    [InlineData("")]
    public async Task InvalidCoordinates_Return422WithoutCallingTheCatalogOrEchoingThem_SEC_TT_API_07_01(string query)
    {
        var hosts = await SecurityHosts.SharedAsync();
        var calls = hosts.Market.Locator.Calls.Count;

        using var response = await SecureHttp.SendAsync(hosts.MarketClient, HttpMethod.Get, _point + "?" + query, bearer: TokenForge.Valid());

        var raw = await SecureHttp.AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "DROP", "<script>", "$gt", "19.43", "-99.13", "Infinity", "NaN");
        raw.Should().Contain("errors");
        hosts.Market.Locator.Calls.Count.Should().Be(calls);
    }

    [Fact]
    public async Task ValidPoint_Returns200WithoutEchoingCoordinatesAndIsNotCacheable_SEC_TT_API_07_02()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var before = hosts.Market.Locator.Calls.Count;

        using var response = await SecureHttp.SendAsync(hosts.MarketClient, HttpMethod.Get, _point + "?lat=20.0512347&lon=-99.3456781", bearer: TokenForge.Valid());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        hosts.Market.Locator.Calls.Count.Should().Be(before + 1);
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("20.05").And.NotContain("99.34");
        var body = System.Text.Json.JsonElement.Parse(raw);
        body.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("enCobertura", "colonia");
        body.GetProperty("colonia").EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "nombre", "cp", "municipio");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.ETag.Should().BeNull();
        response.Content.Headers.LastModified.Should().BeNull();
        response.Headers.Contains("Location").Should().BeFalse();
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Point_OtherMethods_Return405_SEC_TT_API_07_03(string method)
    {
        var hosts = await SecurityHosts.SharedAsync();

        using var response = await SecureHttp.SendAsync(hosts.MarketClient, new HttpMethod(method), _point + "?lat=19.4&lon=-99.1", bearer: TokenForge.Valid());

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task CatalogFailure_Returns500GenericAndLogsNeverContainTheCoordinates_SEC_TT_API_07_04()
    {
        using var factory = await SecurityMarketFactory.CreateAsync();
        factory.Locator.Failure = new InvalidOperationException("catalog down");
        using var client = factory.CreateClient();
        HttpResponseMessage? response = null;

        var output = await ConsoleCapture.RunAsync(async () =>
        {
            response = await SecureHttp.SendAsync(client, HttpMethod.Get, _point + "?lat=20.0512347&lon=-99.3456781", bearer: TokenForge.Valid());
        });

        using (response)
        {
            await SecureHttp.AssertProblemAsync(response!, HttpStatusCode.InternalServerError, "20.0512347", "99.3456781", "catalog down");
        }

        output.Should().NotBeNullOrWhiteSpace();
        output.Should().NotContain("20.0512347").And.NotContain("99.3456781").And.NotContain("lat=");
    }

    [Fact]
    public async Task Point_GlobalLimitReturns429WithoutCallingTheCatalog_SEC_TT_API_07_05()
    {
        using var factory = await SecurityMarketFactory.CreateAsync(new Dictionary<string, string?>
        {
            ["RateLimiting:Global:PermitLimit"] = "2",
            ["RateLimiting:Global:Window"] = "00:01:00",
        });
        using var client = factory.CreateClient();
        var token = TokenForge.Valid();
        for (var i = 0; i < 2; i++)
        {
            using var ok = await SecureHttp.SendAsync(client, HttpMethod.Get, _point + "?lat=20.05&lon=-99.34", bearer: token, remoteIp: "203.0.113.90");
            ok.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var limited = await SecureHttp.SendAsync(client, HttpMethod.Get, _point + "?lat=20.0512347&lon=-99.3456781", bearer: token, remoteIp: "203.0.113.90");

        await SecureHttp.AssertProblemAsync(limited, HttpStatusCode.TooManyRequests, "20.0512347", "99.3456781");
        limited.Headers.Contains("Retry-After").Should().BeTrue();
        factory.Locator.Calls.Should().HaveCount(2);
    }
}
