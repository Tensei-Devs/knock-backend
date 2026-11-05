extern alias IdentityApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using IdentityApi::Knock.Identity.Api.Modules.Sesiones.Data;
using Knock.Gateway.Hosting;
using Knock.IntegrationTest.Support;
using Microsoft.IdentityModel.JsonWebTokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Identity;

/// <summary>Spec 016: renovación y cierre de sesión de punta a punta con MongoDB, Redis y el emulador de Firebase reales. Solo datos ficticios.</summary>
public class RefreshAndLogoutTests
{
    private const string _phone = "+525555550311";
    private static readonly Uri _refreshUri = new("/api/v1/auth/refresh", UriKind.Relative);
    private static readonly Uri _logoutUri = new("/api/v1/auth/logout", UriKind.Relative);

    private static async Task<(string AccessToken, string RefreshToken, string SessionId)> SignInAsync(HttpClient client, string phone = _phone)
    {
        var (idToken, _) = await FirebaseEmulator.SignInWithPhoneAsync(phone);
        using var response = await AuthHttp.ExchangeAsync(client, idToken);
        response.EnsureSuccessStatusCode();
        var (access, refresh) = await AuthHttp.ReadSessionAsync(response);
        return (access, refresh, SessionIdOf(access));
    }

    private static string SessionIdOf(string accessToken) => new JsonWebTokenHandler().ReadJsonWebToken(accessToken).GetClaim("sid").Value;

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string? refreshToken, string? remoteIp = null) =>
        SendAsync(client, new HttpRequestMessage(HttpMethod.Post, _refreshUri) { Content = JsonContent.Create(new { refreshToken }) }, remoteIp);

    private static Task<HttpResponseMessage> LogoutAsync(HttpClient client, string? accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _logoutUri);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return SendAsync(client, request, remoteIp: null);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpRequestMessage request, string? remoteIp)
    {
        using (request)
        {
            if (remoteIp is not null)
            {
                request.Headers.Add(IdentityApiFactory.RemoteIpHeader, remoteIp);
            }

            return await client.SendAsync(request);
        }
    }

    private static async Task<BsonDocument> SessionAsync(IdentityApiFactory factory, string sessionId) =>
        await factory.Sessions.Find(new BsonDocument("_id", ObjectId.Parse(sessionId))).SingleAsync();

    [Fact]
    public async Task ValidToken_Returns200WithANewPairAndTheOldTokenStopsWorking_CA_016_01()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var (_, refresh, sessionId) = await SignInAsync(client);

        using var response = await RefreshAsync(client, refresh);
        using var reused = await RefreshAsync(client, refresh);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("expiresIn").GetInt32().Should().Be(900);
        var newRefresh = body.RootElement.GetProperty("refreshToken").GetString()!;
        newRefresh.Should().NotBe(refresh);
        SessionIdOf(body.RootElement.GetProperty("accessToken").GetString()!).Should().Be(sessionId, "el sid no cambia al renovar");
        reused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var session = await SessionAsync(factory, sessionId);
        session[SesionesStore.UsedFingerprintsField].AsBsonArray.Should().HaveCount(1);
        session.ToJson().Should().NotContain(refresh).And.NotContain(newRefresh);
    }

    [Fact]
    public async Task SimultaneousRotationsWithTheSameToken_OnlyOneWins_CA_016_02()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var (_, refresh, _) = await SignInAsync(client);

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => RefreshAsync(client, refresh)));

        try
        {
            var statuses = string.Join(", ", responses.Select(r => (int)r.StatusCode));
            responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1, $"respuestas: {statuses}");
            responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized).Should().Be(5, $"respuestas: {statuses}");
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task UsedToken_Returns401RevokesEverySessionAndCancelsTheirSids_CA_016_03()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var (_, firstRefresh, firstSid) = await SignInAsync(client);
        var (_, _, secondSid) = await SignInAsync(client);
        using var rotated = await RefreshAsync(client, firstRefresh);
        var rotatedRefresh = (await rotated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refreshToken").GetString();

        using var reuse = await RefreshAsync(client, firstRefresh);
        using var afterReuse = await RefreshAsync(client, rotatedRefresh);

        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        afterReuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "la sesión rotada también quedó revocada");
        (await factory.Sessions.CountDocumentsAsync(new BsonDocument(SesionesStore.RevokedField, false))).Should().Be(0);
        foreach (var sid in new[] { firstSid, secondSid })
        {
            (await factory.Redis.KeyExistsAsync(RedisSesionCancelada.Key(sid))).Should().BeTrue();
            (await factory.Redis.KeyTimeToLiveAsync(RedisSesionCancelada.Key(sid)))!.Value.Should().BePositive().And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(15));
        }
    }

    [Fact]
    public async Task RevokedOrExpiredSession_Returns401_CA_016_04()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var (_, revokedRefresh, revokedSid) = await SignInAsync(client);
        var (_, expiredRefresh, expiredSid) = await SignInAsync(client);
        await factory.Sessions.UpdateOneAsync(
            new BsonDocument("_id", ObjectId.Parse(revokedSid)), Builders<BsonDocument>.Update.Set(SesionesStore.RevokedField, true));
        await factory.Sessions.UpdateOneAsync(
            new BsonDocument("_id", ObjectId.Parse(expiredSid)), Builders<BsonDocument>.Update.Set(SesionesStore.ExpiresAtField, DateTime.UtcNow.AddMinutes(-1)));

        using var revoked = await RefreshAsync(client, revokedRefresh);
        using var expired = await RefreshAsync(client, expiredRefresh);

        revoked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        expired.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SuspendedAccount_CannotRefresh_CA_016_04()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var (_, refresh, _) = await SignInAsync(client);
        await factory.Users.UpdateOneAsync(FilterDefinition<BsonDocument>.Empty, Builders<BsonDocument>.Update.Set(SesionesStore.StateField, "suspendida"));

        using var response = await RefreshAsync(client, refresh);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Returns204RevokesTheSessionAndWritesTheCancellationKey_CA_016_05()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var (access, _, sessionId) = await SignInAsync(client);

        using var response = await LogoutAsync(client, access);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SessionAsync(factory, sessionId))[SesionesStore.RevokedField].AsBoolean.Should().BeTrue();
        var key = RedisSesionCancelada.Key(sessionId);
        (await factory.Redis.KeyExistsAsync(key)).Should().BeTrue();
        var ttl = (await factory.Redis.KeyTimeToLiveAsync(key))!.Value;
        ttl.Should().BePositive().And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(15), "como máximo lo que le queda al JWT");
    }

    [Fact]
    public async Task SessionClosedByLogout_IsSeenAsRevokedByTheGatewayChecker_CA_016_06()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var (access, _, sessionId) = await SignInAsync(client);
        var (_, _, otherSessionId) = await SignInAsync(client);
        var gatewayChecker = new RedisRevokedSessions(factory.Redis.Multiplexer);
        (await gatewayChecker.IsRevokedAsync(sessionId, CancellationToken.None)).Should().BeFalse("antes del logout la sesión vive");

        using var response = await LogoutAsync(client, access);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await gatewayChecker.IsRevokedAsync(sessionId, CancellationToken.None)).Should().BeTrue("el gateway lee la misma clave que escribe Identity");
        (await gatewayChecker.IsRevokedAsync(otherSessionId, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task Logout_OnlyClosesTheSessionOfThatDevice_CA_016_05()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var (access, _, sessionId) = await SignInAsync(client);
        var (_, _, otherSessionId) = await SignInAsync(client);

        using var response = await LogoutAsync(client, access);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SessionAsync(factory, otherSessionId))[SesionesStore.RevokedField].AsBoolean.Should().BeFalse();
        (await factory.Redis.KeyExistsAsync(RedisSesionCancelada.Key(otherSessionId))).Should().BeFalse();
        sessionId.Should().NotBe(otherSessionId);
    }

    [Fact]
    public async Task LogoutWithoutTokenOrWithAnInvalidOne_Returns401_CA_016_07()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var (access, _, _) = await SignInAsync(client);

        using var none = await LogoutAsync(client, null);
        using var garbage = await LogoutAsync(client, "esto-no-es-un-jwt");
        using var tampered = await LogoutAsync(client, access[..^3] + (access.EndsWith("AAA", StringComparison.Ordinal) ? "BBB" : "AAA"));

        none.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        garbage.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        tampered.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await factory.Sessions.CountDocumentsAsync(new BsonDocument(SesionesStore.RevokedField, true))).Should().Be(0);
    }

    [Fact]
    public async Task RefreshAfterLogout_Returns401_CA_016_08()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var (access, refresh, _) = await SignInAsync(client);
        using var logout = await LogoutAsync(client, access);

        using var response = await RefreshAsync(client, refresh);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Responses_AreNotCachedNorCompressedAndDoNotLeakTheReason_CA_016_09()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var (access, refresh, _) = await SignInAsync(client);

        using var ok = await RefreshAsync(client, refresh);
        using var rejected = await RefreshAsync(client, refresh);
        using var logout = await LogoutAsync(client, access);

        foreach (var response in new[] { ok, rejected, logout })
        {
            response.Headers.CacheControl?.NoStore.Should().BeTrue();
            response.Content.Headers.ContentEncoding.Should().BeEmpty();
        }

        var raw = await rejected.Content.ReadAsStringAsync();
        raw.Should().NotContainEquivalentOf("revoked").And.NotContainEquivalentOf("expired").And.NotContainEquivalentOf("reuse").And.NotContain(refresh);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task MissingToken_Returns422_CA_016_01(string? token)
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        using var client = factory.CreateClient();

        using var response = await RefreshAsync(client, token);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task MoreRequestsThanTheAuthLimit_Returns429_CA_016_10()
    {
        await AuthRateLimitCheck.AssertFourthRequestIsLimitedAsync(client => RefreshAsync(client, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "203.0.113.16"));
    }
}
