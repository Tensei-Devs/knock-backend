using System.Net;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using Xunit;

namespace Knock.SecurityTest.Authentication;

/// <summary>
/// Spec 009 (R-05, CA-01, CA-04), specs 016 y 017 y TT-API-07: las cuatro rutas protegidas de los endpoints de negocio (logout, GET y PUT de /me y
/// colonia por punto) rechazan toda credencial inválida con el mismo 401, sin decir el motivo. Host real en Production con MongoDB y Redis reales.
/// </summary>
public class BearerValidationTests
{
    [Theory]
    [MemberData(nameof(RouteData))]
    public async Task WithoutAuthorizationHeader_Returns401ProblemDetails_SEC_009_01(string route)
    {
        var hosts = await SecurityHosts.SharedAsync();
        using var response = await ProtectedRoutes.SendAsync(hosts, route, authorization: null);

        await AssertUniform401Async(response);
    }

    [Theory]
    [MemberData(nameof(RouteAttackData))]
    public async Task ForgedOrBrokenToken_Returns401WithoutSayingWhy_SEC_009_02(string route, string attack)
    {
        var hosts = await SecurityHosts.SharedAsync();
        using var response = await ProtectedRoutes.SendBearerAsync(hosts, route, TokenForge.Attacks[attack]());

        await AssertUniform401Async(response);
    }

    [Theory]
    [MemberData(nameof(RouteData))]
    public async Task EveryRejectionOfARouteHasTheSameBody_SEC_009_03(string route)
    {
        var hosts = await SecurityHosts.SharedAsync();
        var bodies = new HashSet<string>(StringComparer.Ordinal);
        var challenges = new HashSet<string>(StringComparer.Ordinal);
        foreach (var attack in TokenForge.Attacks.Values.Select(make => make()).Prepend(string.Empty))
        {
            using var response = await ProtectedRoutes.SendAsync(hosts, route, attack.Length == 0 ? null : "Bearer " + attack);
            bodies.Add(SecureHttp.WithoutCorrelationId(await response.Content.ReadAsStringAsync()));
            challenges.Add(string.Join(",", response.Headers.WwwAuthenticate.Select(c => c.ToString())));
        }

        bodies.Should().ContainSingle("el cuerpo del 401 no depende del motivo del rechazo");
        challenges.Should().ContainSingle().Which.Should().Be("Bearer", "sin error=\"invalid_token\" ni descripción del motivo");
    }

    [Theory]
    [MemberData(nameof(RouteData))]
    public async Task OtherAuthorizationSchemes_AreNotAccepted_SEC_009_04(string route)
    {
        var hosts = await SecurityHosts.SharedAsync();
        var valid = TokenForge.Valid();
        foreach (var header in new[] { "Basic dXN1YXJpbzpjbGF2ZQ==", "Token " + valid, valid, "Bearer", "Bearer ", "Bearer  ", "Negotiate " + valid, "Digest " + valid })
        {
            using var response = await ProtectedRoutes.SendAsync(hosts, route, header);

            await AssertUniform401Async(response);
        }
    }

    [Theory]
    [MemberData(nameof(RouteData))]
    public async Task TokenInTheQueryStringOrACookie_IsIgnored_SEC_009_05(string route)
    {
        var hosts = await SecurityHosts.SharedAsync();
        var valid = TokenForge.Valid();
        var (client, path) = route switch
        {
            ProtectedRoutes.Colonias => (hosts.MarketClient, "/api/v1/colonias/en-punto?lat=19.4326&lon=-99.1332&access_token="),
            ProtectedRoutes.Logout => (hosts.IdentityClient, "/api/v1/auth/logout?access_token="),
            _ => (hosts.IdentityClient, "/api/v1/me?access_token="),
        };
        var method = new HttpMethod(route.Split(' ')[0]);
        using var request = new HttpRequestMessage(method, new Uri(path + valid, UriKind.Relative));
        request.Headers.TryAddWithoutValidation("Cookie", $"access_token={valid}; Authorization=Bearer {valid}");
        request.Headers.TryAddWithoutValidation("X-Access-Token", valid);

        using var response = await client.SendAsync(request);

        await AssertUniform401Async(response);
    }

    [Theory]
    [MemberData(nameof(RouteData))]
    public async Task ValidToken_IsNotRejected_ControlOfTheNegativeCases_SEC_009_06(string route)
    {
        var hosts = await SecurityHosts.SharedAsync();
        // Control: la misma fábrica de tokens produce uno aceptado, así los 401 de arriba vienen del ataque y no de un token mal armado.
        using var response = await ProtectedRoutes.SendBearerAsync(hosts, route, TokenForge.Valid());

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        ((int)response.StatusCode).Should().BeLessThan(500);
    }

    [Fact]
    public async Task SignedTokenWithoutSubject_Returns401OnProfileRoutes_SEC_009_07()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var token = TokenForge.WithoutSub();

        using var read = await ProtectedRoutes.SendBearerAsync(hosts, ProtectedRoutes.MeGet, token);
        using var write = await ProtectedRoutes.SendBearerAsync(hosts, ProtectedRoutes.MePut, token);

        await SecureHttp.AssertProblemAsync(read, HttpStatusCode.Unauthorized);
        await SecureHttp.AssertProblemAsync(write, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignedTokenWithoutSessionOrSubject_Returns401OnLogoutAndRevokesNothing_SEC_009_08()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var person = await hosts.Identity.SignInAsync(hosts.IdentityClient);

        using var withoutSid = await ProtectedRoutes.SendBearerAsync(hosts, ProtectedRoutes.Logout, TokenForge.WithoutSid());
        using var withoutSub = await ProtectedRoutes.SendBearerAsync(hosts, ProtectedRoutes.Logout, TokenForge.WithoutSub());

        await SecureHttp.AssertProblemAsync(withoutSid, HttpStatusCode.Unauthorized);
        await SecureHttp.AssertProblemAsync(withoutSub, HttpStatusCode.Unauthorized);
        (await hosts.Identity.Redis.KeyExistsAsync("sesion-cancelada:" + person.SessionId)).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(RouteData))]
    public async Task Rejections_CarryTheSecurityHeadersAndNoStore_SEC_009_09(string route)
    {
        var hosts = await SecurityHosts.SharedAsync();
        using var response = await ProtectedRoutes.SendBearerAsync(hosts, route, TokenForge.AlgNone());

        SecureHttp.AssertNotCacheable(response);
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle().Which.Should().Be("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle().Which.Should().Be("no-referrer");
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle().Which.Should().Contain("default-src 'none'");
        response.Headers.GetValues("X-Correlation-Id").Should().ContainSingle();
    }

    public static TheoryData<string> RouteData() => ProtectedRoutes.Routes();

    public static TheoryData<string, string> RouteAttackData() => ProtectedRoutes.RoutesTimesAttacks();

    private static async Task AssertUniform401Async(HttpResponseMessage response)
    {
        var raw = await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Select(c => c.ToString()).Should().ContainSingle().Which.Should().Be("Bearer");
        raw.Should().NotContain("Bearer ", "el cuerpo no repite la credencial recibida");
    }
}
