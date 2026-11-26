using System.Net;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.SecurityTest.Identity;

/// <summary>
/// Spec 025, CA-05, CA-06 y CA-17: pruebas de seguridad en Production con MongoDB/Redis reales.
/// Solo pruebas de 401 (sin token / vencido / alg-none). No se prueba creación con fotos (T-04 pendiente).
/// </summary>
public class Spec025VerificacionSecurityTests
{
    private const string _route = "/api/v1/me/verificacion";

    [Fact]
    public async Task Sin_Token_Returns401_CA_025_17()
    {
        var hosts = await SecurityHosts.SharedAsync();
        using var response = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Get, _route);
        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_Vencido_Returns401_CA_025_17()
    {
        var hosts = await SecurityHosts.SharedAsync();
        using var response = await SecureHttp.SendAsync(
            hosts.IdentityClient, HttpMethod.Get, _route, bearer: TokenForge.Expired());

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_Alg_None_Returns401_CA_025_17()
    {
        var hosts = await SecurityHosts.SharedAsync();
        using var response = await SecureHttp.SendAsync(
            hosts.IdentityClient, HttpMethod.Get, _route, bearer: TokenForge.AlgNone());

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }
}