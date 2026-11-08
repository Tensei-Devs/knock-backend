extern alias IdentityApi;

using System.Net;
using AwesomeAssertions;
using IdentityApi::Knock.Identity.Api.Modules.Usuarios.Services;
using Knock.IntegrationTest.Support;
using Knock.SecurityTest.Support;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Knock.SecurityTest.Market;

/// <summary>
/// Spec 017, R-06: <c>GET /api/v1/colonias/existe</c> de Mercado con sesión, entradas hostiles y el cliente real de Identidad contra el host real
/// de Mercado (MongoDB real). Solo datos ficticios.
/// </summary>
public class ColoniaExistsSecurityTests
{
    private const string _exists = "/api/v1/colonias/existe";

    private static MarketColoniaCatalog CatalogWith(HttpClient market, string? authorization)
    {
        var context = new DefaultHttpContext();
        if (authorization is not null)
        {
            context.Request.Headers.Authorization = authorization;
        }

        return new MarketColoniaCatalog(market, new HttpContextAccessor { HttpContext = context });
    }

    [Fact]
    public async Task WithoutASession_Returns401_SEC_017_20()
    {
        var hosts = await SecurityHosts.SharedAsync();

        using var none = await SecureHttp.SendAsync(hosts.MarketClient, HttpMethod.Get, _exists + "?nombre=Tultengo");
        using var forged = await SecureHttp.SendAsync(hosts.MarketClient, HttpMethod.Get, _exists + "?nombre=Tultengo", bearer: TokenForge.AlgNone());

        none.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        forged.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Tultengo", true)]
    [InlineData("tultengo", true)]
    [InlineData("TÚLTENGO", true)]
    [InlineData("Colonia Inventada", false)]
    public async Task WithASession_AnswersOnlyTheBoolean_SEC_017_21(string name, bool expected)
    {
        var hosts = await SecurityHosts.SharedAsync();

        using var response = await SecureHttp.SendAsync(hosts.MarketClient, HttpMethod.Get, _exists + "?nombre=" + Uri.EscapeDataString(name), bearer: TokenForge.Valid());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await SecureHttp.ReadJsonAsync(response);
        body.EnumerateObject().Select(p => p.Name).Should().Equal("existe");
        body.GetProperty("existe").GetBoolean().Should().Be(expected);
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("nombre=")]
    [InlineData("nombre=%3Cscript%3E")]
    [InlineData("nombre=%7B%22%24ne%22%3Anull%7D")]
    [InlineData("nombre=Tultengo%0A")]
    [InlineData("nombre=Tultengo%0d%0aX-Injected:1")]
    [InlineData("nombre=Tultengo&nombre=Centro")]
    [InlineData("nombre[$ne]=x")]
    [InlineData("otro=Tultengo")]
    public async Task InvalidName_Returns422WithoutEchoingIt_SEC_017_22(string query)
    {
        var hosts = await SecurityHosts.SharedAsync();

        using var response = await SecureHttp.SendAsync(hosts.MarketClient, HttpMethod.Get, _exists + "?" + query, bearer: TokenForge.Valid());

        var raw = await SecureHttp.AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "script", "$ne", "X-Injected", "Tultengo", "Centro");
        raw.Should().Contain("errors");
    }

    [Fact]
    public async Task NameOverTheLimit_Returns422_SEC_017_23()
    {
        var hosts = await SecurityHosts.SharedAsync();

        using var response = await SecureHttp.SendAsync(hosts.MarketClient, HttpMethod.Get, _exists + "?nombre=" + new string('a', 81), bearer: TokenForge.Valid());

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Writes_Return405_SEC_017_24(string method)
    {
        var hosts = await SecurityHosts.SharedAsync();

        using var response = await SecureHttp.SendAsync(hosts.MarketClient, new HttpMethod(method), _exists + "?nombre=Tultengo", bearer: TokenForge.Valid());

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.MethodNotAllowed);
    }

    [Theory]
    [InlineData("Tultengo", true)]
    [InlineData("tultengo", true)]
    [InlineData("Colonia Inventada", false)]
    public async Task IdentityClient_AgainstTheRealMarket_GetsTheRightAnswer_CA_017_07(string name, bool expected)
    {
        var hosts = await SecurityHosts.SharedAsync();
        var catalog = CatalogWith(hosts.MarketClient, "Bearer " + TokenForge.Valid());

        var exists = await catalog.ExistsAsync(name, CancellationToken.None);

        exists.Should().Be(expected);
    }

    [Fact]
    public async Task IdentityClient_WithASessionMarketRejects_FailsClosed_CA_017_12()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var catalog = CatalogWith(hosts.MarketClient, "Bearer " + TokenForge.AlgNone());

        var act = () => catalog.ExistsAsync("Tultengo", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>("Mercado responde 401 y eso nunca se toma por «la colonia existe»");
    }
}
