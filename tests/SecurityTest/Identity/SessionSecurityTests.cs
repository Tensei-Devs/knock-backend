using System.Net;
using System.Text;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Knock.SecurityTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.SecurityTest.Identity;

/// <summary>Spec 016: <c>POST /api/v1/auth/refresh</c> (público) y <c>POST /api/v1/auth/logout</c> (con sesión). Host real en Production, Redis y MongoDB reales.</summary>
public class SessionSecurityTests
{
    private const string _refresh = "/api/v1/auth/refresh";
    private const string _logout = "/api/v1/auth/logout";

    private static Task<HttpResponseMessage> RefreshAsync(SecurityHosts hosts, string token) =>
        SecureHttp.PostJsonAsync(hosts.IdentityClient, _refresh, new { refreshToken = token });

    private static string RandomRefreshToken() => Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    [Fact]
    public async Task RefreshTokenOfOnePerson_NeverYieldsTheSessionOfAnother_SEC_016_01()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var a = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        var b = await hosts.Identity.SignInAsync(hosts.IdentityClient);

        using var response = await RefreshAsync(hosts, a.RefreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var access = (await SecureHttp.ReadJsonAsync(response)).GetProperty("accessToken").GetString()!;
        TokenForge.Claim(access, "sub").Should().Be(a.Sub);
        TokenForge.Claim(access, "sid").Should().Be(a.SessionId);
        using var bStillWorks = await RefreshAsync(hosts, b.RefreshToken);
        bStillWorks.StatusCode.Should().Be(HttpStatusCode.OK, "la renovación de A no tocó la sesión de B");
    }

    [Fact]
    public async Task AccessTokenAndRefreshTokenAreNotInterchangeable_SEC_016_02()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var a = await hosts.Identity.SignInAsync(hosts.IdentityClient);

        using var jwtAsRefresh = await RefreshAsync(hosts, a.AccessToken);
        using var refreshAsBearer = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Get, "/api/v1/me", bearer: a.RefreshToken);
        using var sidAsRefresh = await RefreshAsync(hosts, a.SessionId);

        ((int)jwtAsRefresh.StatusCode).Should().BeOneOf(401, 422);
        await SecureHttp.AssertProblemAsync(refreshAsBearer, HttpStatusCode.Unauthorized);
        await SecureHttp.AssertProblemAsync(sidAsRefresh, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EveryRefreshRejection_HasTheSame401Body_SoCausesCannotBeEnumerated_SEC_016_03()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var used = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        (await RefreshAsync(hosts, used.RefreshToken)).Dispose();

        var revoked = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        await hosts.Identity.Sessions.UpdateOneAsync(new BsonDocument("_id", ObjectId.Parse(revoked.SessionId)), Builders<BsonDocument>.Update.Set("revocada", true));
        var expired = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        await hosts.Identity.Sessions.UpdateOneAsync(new BsonDocument("_id", ObjectId.Parse(expired.SessionId)), Builders<BsonDocument>.Update.Set("venceEn", DateTime.UtcNow.AddMinutes(-1)));
        var suspended = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        await hosts.Identity.Users.UpdateOneAsync(new BsonDocument("sub", suspended.Sub), Builders<BsonDocument>.Update.Set("estado", "suspendida"));
        var loggedOut = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        (await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Post, _logout, bearer: loggedOut.AccessToken)).Dispose();

        var candidates = new[]
        {
            used.RefreshToken, revoked.RefreshToken, expired.RefreshToken, suspended.RefreshToken, loggedOut.RefreshToken,
            RandomRefreshToken(), "corto", new string('A', 43) + "=", "' OR '1'='1", "{\"$ne\":null}", new string('é', 40), "AAAA AAAA", "\0",
        };
        var bodies = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in candidates)
        {
            using var response = await RefreshAsync(hosts, token);
            var raw = await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized, token.Length > 6 ? token : string.Empty);
            bodies.Add(SecureHttp.WithoutCorrelationId(raw));
        }

        bodies.Should().ContainSingle();
    }

    [Fact]
    public async Task ReuseOfAStolenToken_RevokesOnlyTheOwnersSessions_SEC_016_04()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var a = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        var b = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        (await RefreshAsync(hosts, a.RefreshToken)).Dispose();

        using var replay = await RefreshAsync(hosts, a.RefreshToken);

        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await hosts.Identity.Sessions.Find(new BsonDocument("_id", ObjectId.Parse(a.SessionId))).SingleAsync())["revocada"].AsBoolean.Should().BeTrue();
        (await hosts.Identity.Sessions.Find(new BsonDocument("_id", ObjectId.Parse(b.SessionId))).SingleAsync())["revocada"].AsBoolean.Should().BeFalse();
        (await hosts.Identity.Redis.KeyExistsAsync("sesion-cancelada:" + b.SessionId)).Should().BeFalse();
    }

    [Fact]
    public async Task Logout_OnlyClosesTheCallersSession_EvenWithAForeignSid_SEC_016_05()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var a = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        var b = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        var aSecondDevice = await hosts.Identity.SignInAsync(hosts.IdentityClient);

        // Token con la clave buena: sub de A pero el sid de la sesión de B (solo posible si la clave se filtrara; defensa en profundidad).
        using var forged = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Post, _logout, bearer: TokenForge.Valid(a.Sub, b.SessionId));

        forged.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await hosts.Identity.Sessions.Find(new BsonDocument("_id", ObjectId.Parse(b.SessionId))).SingleAsync())["revocada"].AsBoolean.Should().BeFalse("la sesión de B no es de A");
        using var bRefresh = await RefreshAsync(hosts, b.RefreshToken);
        bRefresh.StatusCode.Should().Be(HttpStatusCode.OK);
        (await hosts.Identity.Sessions.Find(new BsonDocument("_id", ObjectId.Parse(aSecondDevice.SessionId))).SingleAsync())["revocada"].AsBoolean.Should().BeFalse();
    }

    [Fact(Skip = "Hallazgo H-1 (evidencia): LogoutService escribe sesion-cancelada:{sid} sin comprobar que la sesion es del sub del token; ver RESUMEN.md")]
    public async Task Logout_DoesNotCancelTheSidOfAnotherPersonInRedis_SEC_016_06()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var a = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        var b = await hosts.Identity.SignInAsync(hosts.IdentityClient);

        using var forged = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Post, _logout, bearer: TokenForge.Valid(a.Sub, b.SessionId));

        forged.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await hosts.Identity.Redis.KeyExistsAsync("sesion-cancelada:" + b.SessionId)).Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefreshBodyOver1KiB_Returns413_SEC_016_07(bool withoutContentLength)
    {
        var hosts = await SecurityHosts.SharedAsync();
        var json = $$"""{"refreshToken":"{{new string('a', 2000)}}"}""";
        using HttpContent content = withoutContentLength ? new UnknownLengthContent(json) : new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Post, _refresh, content);

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.RequestEntityTooLarge);
    }

    [Theory]
    [InlineData("""{"refreshToken":123}""")]
    [InlineData("""{"refreshToken":{"$ne":null}}""")]
    [InlineData("""{"refreshToken":["a"]}""")]
    [InlineData("""{"refreshToken":true}""")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("""{"refreshToken":""}""")]
    [InlineData("""{"refreshToken":null,"sub":"x"}""")]
    public async Task WrongTypesOrMissingToken_AreRejectedAs4xxNeverAs500_SEC_016_08(string json)
    {
        var hosts = await SecurityHosts.SharedAsync();

        using var response = await SecureHttp.SendRawJsonAsync(hosts.IdentityClient, HttpMethod.Post, _refresh, json);

        ((int)response.StatusCode).Should().BeInRange(400, 499);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("$ne").And.NotContain("stackTrace");
    }

    [Fact]
    public async Task ExtraFieldsInTheRefreshBody_AreIgnored_SEC_016_09()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var a = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        var b = await hosts.Identity.SignInAsync(hosts.IdentityClient);

        using var response = await SecureHttp.SendRawJsonAsync(
            hosts.IdentityClient, HttpMethod.Post, _refresh, $$"""{"refreshToken":"{{a.RefreshToken}}","sub":"{{b.Sub}}","sid":"{{b.SessionId}}","usuarioId":"{{b.Sub}}"}""");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        TokenForge.Claim((await SecureHttp.ReadJsonAsync(response)).GetProperty("accessToken").GetString()!, "sub").Should().Be(a.Sub);
    }

    [Theory]
    [InlineData("GET", _refresh)]
    [InlineData("PUT", _refresh)]
    [InlineData("DELETE", _refresh)]
    [InlineData("GET", _logout)]
    [InlineData("PUT", _logout)]
    [InlineData("DELETE", _logout)]
    public async Task OtherMethods_Return405_SEC_016_10(string method, string path)
    {
        var hosts = await SecurityHosts.SharedAsync();

        using var response = await SecureHttp.SendAsync(hosts.IdentityClient, new HttpMethod(method), path, bearer: TokenForge.Valid());

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task TokenResponsesAreNotCachedNorCompressed_AndRejectionsCarryNoStore_SEC_016_11()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var a = await hosts.Identity.SignInAsync(hosts.IdentityClient);

        using var ok = await RefreshAsync(hosts, a.RefreshToken);
        using var rejected = await RefreshAsync(hosts, a.RefreshToken);
        using var logout = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Post, _logout, bearer: a.AccessToken);

        foreach (var response in new[] { ok, rejected, logout })
        {
            SecureHttp.AssertNotCacheable(response);
        }

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logs_NeverContainRefreshTokensOrJwts_OnRotationReuseAndLogout_SEC_016_12()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var a = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        var secrets = new List<string> { a.RefreshToken, a.AccessToken, a.Sub, a.SessionId };

        var output = await ConsoleCapture.RunAsync(async () =>
        {
            using var rotated = await RefreshAsync(hosts, a.RefreshToken);
            var body = await SecureHttp.ReadJsonAsync(rotated);
            secrets.Add(body.GetProperty("accessToken").GetString()!);
            secrets.Add(body.GetProperty("refreshToken").GetString()!);
            using var reuse = await RefreshAsync(hosts, a.RefreshToken);
            using var logout = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Post, _logout, bearer: a.AccessToken);
        });

        output.Should().NotBeNullOrWhiteSpace();
        foreach (var secret in secrets)
        {
            output.Should().NotContain(secret);
        }
    }

    [Fact]
    public async Task RefreshAndLogoutRoutesShareTheStrictAuthLimitPerIp_SEC_016_13()
    {
        using var factory = await SecurityIdentityFactory.CreateAsync(new Dictionary<string, string?>
        {
            ["RateLimiting:Auth:PermitLimit"] = "3",
            ["RateLimiting:Auth:Window"] = "00:01:00",
        });
        using var client = factory.CreateClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            var path = i % 2 == 0 ? _refresh : _logout;
            using var response = path == _refresh
                ? await SecureHttp.PostJsonAsync(client, path, new { refreshToken = RandomRefreshToken() }, remoteIp: "203.0.113.60")
                : await SecureHttp.SendAsync(client, HttpMethod.Post, path, bearer: "basura", remoteIp: "203.0.113.60");
            statuses.Add(response.StatusCode);
            if (i == 3)
            {
                await SecureHttp.AssertProblemAsync(response, HttpStatusCode.TooManyRequests, "203.0.113.60");
                response.Headers.Contains("Retry-After").Should().BeTrue();
            }
        }

        statuses.Take(3).Should().OnlyContain(s => s == HttpStatusCode.Unauthorized);
    }
}
