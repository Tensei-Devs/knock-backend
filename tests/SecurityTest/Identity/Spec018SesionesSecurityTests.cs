using System.Net;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.SecurityTest.Identity;

/// <summary>
/// Spec 018, CA-01, CA-08: pruebas de seguridad de sesiones.
/// </summary>
public class Spec018SesionesSecurityTests
{
    private const string _route = "/api/v1/me/sesiones";

    [Fact]
    public async Task Sin_Token_GET_Returns401_CA_018_08()
    {
        var hosts = await SecurityHosts.SharedAsync();
        using var response = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Get, _route);
        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_Vencido_GET_Returns401_CA_018_08()
    {
        var hosts = await SecurityHosts.SharedAsync();
        using var response = await SecureHttp.SendAsync(
            hosts.IdentityClient, HttpMethod.Get, _route, bearer: TokenForge.Expired());
        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sin_Token_Delete_Returns401_CA_018_08()
    {
        var hosts = await SecurityHosts.SharedAsync();
        using var response = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Delete, _route);
        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }
}