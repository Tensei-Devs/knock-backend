using System.Net;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.SecurityTest.Market;

/// <summary>
/// Spec 042, CA-05, CA-10: pruebas de seguridad sin token.
/// </summary>
public class Spec042ConfianzaSecurityTests
{
    private const string _route = "/api/v1/me/tecnicos-confianza";

    [Fact]
    public async Task Sin_Token_GET_Returns401_CA_042_05()
    {
        var hosts = await SecurityHosts.SharedAsync();
        using var response = await SecureHttp.SendAsync(hosts.MarketClient, HttpMethod.Get, _route);
        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_Vencido_GET_Returns401_CA_042_05()
    {
        var hosts = await SecurityHosts.SharedAsync();
        using var response = await SecureHttp.SendAsync(
            hosts.MarketClient, HttpMethod.Get, _route, bearer: TokenForge.Expired());
        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sin_Token_Put_Returns401()
    {
        var hosts = await SecurityHosts.SharedAsync();
        using var response = await SecureHttp.SendAsync(hosts.MarketClient, HttpMethod.Put, $"{_route}/some-id");
        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sin_Token_Delete_Returns401()
    {
        var hosts = await SecurityHosts.SharedAsync();
        using var response = await SecureHttp.SendAsync(hosts.MarketClient, HttpMethod.Delete, $"{_route}/some-id");
        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }
}