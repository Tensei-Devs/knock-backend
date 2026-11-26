using System.Net;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.SecurityTest.Identity;

/// <summary>
/// Spec 040, CA-02, CA-04, CA-08: pruebas de seguridad sin token.
/// </summary>
public class Spec040DireccionesSecurityTests
{
    private const string _route = "/api/v1/me/direcciones";

    [Fact]
    public async Task Sin_Token_GET_Returns401_CA_040_02()
    {
        var hosts = await SecurityHosts.SharedAsync();
        using var response = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Get, _route);
        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_Vencido_GET_Returns401_CA_040_02()
    {
        var hosts = await SecurityHosts.SharedAsync();
        using var response = await SecureHttp.SendAsync(
            hosts.IdentityClient, HttpMethod.Get, _route, bearer: TokenForge.Expired());
        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sin_Token_Post_Returns401()
    {
        var hosts = await SecurityHosts.SharedAsync();
        using var response = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Post, _route);
        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sin_Token_Put_Returns401()
    {
        var hosts = await SecurityHosts.SharedAsync();
        using var response = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Put, $"{_route}/some-id");
        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sin_Token_Delete_Returns401()
    {
        var hosts = await SecurityHosts.SharedAsync();
        using var response = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Delete, $"{_route}/some-id");
        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }
}