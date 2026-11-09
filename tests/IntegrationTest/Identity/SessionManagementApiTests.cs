extern alias IdentityApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using IdentityApi::Knock.Identity.Api.Modules.Sesiones.Data;
using Knock.Gateway.Hosting;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;
using Xunit;

namespace Knock.IntegrationTest.Identity;

public sealed class SessionManagementApiTests
{
    private const string _phone = "+525555552131";
    private const string _otherPhone = "+525555552132";
    private static readonly Uri _sessionsUri = new("/api/v1/me/sesiones", UriKind.Relative);
    private static readonly string[] _sessionProperties = ["id", "dispositivo", "ultimaActividad", "actual"];

    private static async Task<IdentityApiFactory> CreateFactoryAsync()
    {
        var redis = await RedisTestFixture.SharedAsync();
        return await IdentityApiFactory.CreateAsync(new Dictionary<string, string?>
        {
            ["ConnectionStrings:KnockRedis"] = redis.ConnectionString,
        });
    }

    private static async Task<string> SignInAsync(HttpClient client, string phone)
    {
        var (idToken, _) = await FirebaseEmulator.SignInWithPhoneAsync(phone);
        using var response = await AuthHttp.ExchangeAsync(client, idToken);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        return (await AuthHttp.ReadSessionAsync(response)).AccessToken;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        Uri uri,
        string? accessToken = null)
    {
        using var request = new HttpRequestMessage(method, uri);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await client.SendAsync(request);
    }

    private static string SessionId(string accessToken) =>
        new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler()
            .ReadJsonWebToken(accessToken)
            .GetClaim("sid").Value;

    [Fact]
    public async Task ListSessions_ReturnsOnlyOwnOpenSessionsInContractShape_CA_018_01_CA_018_02_CA_018_03()
    {
        using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var tokens = new[]
        {
            await SignInAsync(client, _phone),
            await SignInAsync(client, _phone),
            await SignInAsync(client, _phone),
        };
        var revokedToken = await SignInAsync(client, _phone);
        var expiredToken = await SignInAsync(client, _phone);
        await SignInAsync(client, _otherPhone);
        await factory.Sessions.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq(SesionesStore.IdField, ObjectId.Parse(SessionId(revokedToken))),
            Builders<BsonDocument>.Update.Set(SesionesStore.RevokedField, true));
        await factory.Sessions.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq(SesionesStore.IdField, ObjectId.Parse(SessionId(expiredToken))),
            Builders<BsonDocument>.Update.Set(SesionesStore.ExpiresAtField, DateTime.UtcNow.AddMinutes(-1)));

        using var response = await SendAsync(client, HttpMethod.Get, _sessionsUri, tokens[^1]);
        var raw = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl?.Private.Should().BeTrue();
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
        using var body = JsonDocument.Parse(raw);
        var sessions = body.RootElement.EnumerateArray().ToArray();
        sessions.Should().HaveCount(3);
        sessions.Select(element => element.GetProperty("actual").GetBoolean()).Should().ContainSingle(value => value);
        sessions.Single(element => element.GetProperty("actual").GetBoolean()).GetProperty("id").GetString().Should().Be(SessionId(tokens[^1]));
        sessions.Select(element => element.GetProperty("ultimaActividad").GetDateTime()).Should().BeInDescendingOrder();
        sessions.Should().OnlyContain(element =>
            element.EnumerateObject().Select(property => property.Name)
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(_sessionProperties));
        sessions.Select(element => element.GetProperty("ultimaActividad").GetDateTime().Kind)
            .Should().OnlyContain(kind => kind == DateTimeKind.Utc);
        raw.Should().NotContain("huella");
        raw.Should().NotContain("refreshToken");
        raw.Should().NotContain("accessToken");
    }

    [Fact]
    public async Task ListSessions_WhenNoSessionDocumentsRemain_ReturnsEmptyArrayAndNoStore_CA_018_02()
    {
        using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var token = await SignInAsync(client, _phone);
        await factory.Sessions.DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);

        using var response = await SendAsync(client, HttpMethod.Get, _sessionsUri, token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
        (await response.Content.ReadAsStringAsync()).Should().Be("[]");
    }

    [Fact]
    public async Task SessionEndpoints_WithoutToken_Return401_CA_018_08()
    {
        using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();

        using var list = await SendAsync(client, HttpMethod.Get, _sessionsUri);
        using var revoke = await SendAsync(
            client,
            HttpMethod.Delete,
            new Uri("/api/v1/me/sesiones/665f1b2c3d4e5f6071829301", UriKind.Relative));

        list.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        revoke.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RevokeOwnOtherSession_MarksMongoAndRedisAndGatewayRejectsItsJwt_CA_018_04()
    {
        var redis = await RedisTestFixture.SharedAsync();
        using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var currentToken = await SignInAsync(client, _phone);
        var otherToken = await SignInAsync(client, _phone);
        var targetId = SessionId(otherToken);
        var gatewaySettings = new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = IdentityApiFactory.SigningKeyBase64,
            ["ConnectionStrings:KnockRedis"] = redis.ConnectionString,
        };
        await using var gateway = await GatewayHost.StartAsync(gatewaySettings);
        var protectedUri = new Uri("/api/v1/colonias/en-punto", UriKind.Relative);

        using var before = await SendAsync(gateway.Client, HttpMethod.Get, protectedUri, otherToken);
        before.StatusCode.Should().Be(HttpStatusCode.OK);

        using var response = await SendAsync(
            client,
            HttpMethod.Delete,
            new Uri($"/api/v1/me/sesiones/{targetId}", UriKind.Relative),
            currentToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
        var storedSession = await factory.Sessions.Find(Builders<BsonDocument>.Filter.Eq(SesionesStore.IdField, ObjectId.Parse(targetId))).SingleAsync();
        storedSession[SesionesStore.RevokedField].AsBoolean.Should().BeTrue();
        await using (var connection = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString))
        {
            (await connection.GetDatabase().KeyExistsAsync(RedisRevokedSessions.KeyPrefix + targetId)).Should().BeTrue();
        }

        using var after = await SendAsync(gateway.Client, HttpMethod.Get, protectedUri, otherToken);
        after.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var repeated = await SendAsync(
            client,
            HttpMethod.Delete,
            new Uri($"/api/v1/me/sesiones/{targetId}", UriKind.Relative),
            currentToken);
        repeated.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RevokeForeignSession_ReturnsSame404AndDoesNotMutateIt_CA_018_05()
    {
        using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var callerToken = await SignInAsync(client, _phone);
        var foreignToken = await SignInAsync(client, _otherPhone);
        var foreignId = SessionId(foreignToken);

        using var response = await SendAsync(
            client,
            HttpMethod.Delete,
            new Uri($"/api/v1/me/sesiones/{foreignId}", UriKind.Relative),
            callerToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var foreignSession = await factory.Sessions.Find(Builders<BsonDocument>.Filter.Eq(SesionesStore.IdField, ObjectId.Parse(foreignId))).SingleAsync();
        foreignSession[SesionesStore.RevokedField].AsBoolean.Should().BeFalse();
    }

    [Theory]
    [InlineData("invalid-object-id", HttpStatusCode.BadRequest)]
    [InlineData("665f1b2c3d4e5f6071829301", HttpStatusCode.NotFound)]
    public async Task RevokeInvalidOrMissingSession_ReturnsContractStatus_CA_018_06_CA_018_07(
        string id,
        HttpStatusCode expectedStatus)
    {
        using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var callerToken = await SignInAsync(client, _phone);

        using var response = await SendAsync(
            client,
            HttpMethod.Delete,
            new Uri($"/api/v1/me/sesiones/{id}", UriKind.Relative),
            callerToken);

        response.StatusCode.Should().Be(expectedStatus);
    }

    [Fact]
    public async Task RevokeCurrentSession_Returns409_SPEC_018_DECISION()
    {
        using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var currentToken = await SignInAsync(client, _phone);
        var currentId = SessionId(currentToken);

        using var response = await SendAsync(
            client,
            HttpMethod.Delete,
            new Uri($"/api/v1/me/sesiones/{currentId}", UriKind.Relative),
            currentToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RevokedSession_CannotBeRenewed_CA_018_09()
    {
        using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var currentToken = await SignInAsync(client, _phone);
        var (idToken, _) = await FirebaseEmulator.SignInWithPhoneAsync(_phone);
        using var exchange = await AuthHttp.ExchangeAsync(client, idToken);
        var (targetToken, refreshToken) = await AuthHttp.ReadSessionAsync(exchange);
        var targetId = SessionId(targetToken);

        using var revoked = await SendAsync(
            client,
            HttpMethod.Delete,
            new Uri($"/api/v1/me/sesiones/{targetId}", UriKind.Relative),
            currentToken);
        using var refreshRequest = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri("/api/v1/auth/refresh", UriKind.Relative))
        {
            Content = JsonContent.Create(new { refreshToken }),
        };
        using var refreshed = await client.SendAsync(refreshRequest);

        revoked.StatusCode.Should().Be(HttpStatusCode.NoContent);
        refreshed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var problem = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("status").GetInt32().Should().Be((int)HttpStatusCode.Unauthorized);
        problem.RootElement.GetProperty("title").GetString().Should().Be("Unauthorized");
        problem.RootElement.GetProperty("detail").GetString().Should().Be("Invalid credentials.");
        var session = await factory.Sessions.Find(
            Builders<BsonDocument>.Filter.Eq(SesionesStore.IdField, ObjectId.Parse(targetId))).SingleAsync();
        session[SesionesStore.RevokedField].AsBoolean.Should().BeTrue();
    }
}
