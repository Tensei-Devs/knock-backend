extern alias IdentityApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using IdentityApi::Knock.Identity.Api.Modules.Sesiones.Data;
using Knock.IntegrationTest.Support;
using Microsoft.IdentityModel.JsonWebTokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Identity;

/// <summary>
/// Spec 037, R-13 y CA-18: Identidad deja <c>sesion_revocada { sid }</c> en la bandeja en cada vía en que una sesión termina (cierre, cierre remoto, expulsión por el límite y reuso del token de
/// renovación), en la misma transacción que la revoca. MongoDB, Redis y el emulador de Firebase reales; solo datos ficticios.
/// </summary>
public sealed class Spec037SesionRevocadaTests
{
    private const string _phone = "+525555553711";
    private const string _eventType = "sesion_revocada";
    private static readonly string[] _payloadFields = ["eventoId", "sid"];

    private static async Task<IdentityApiFactory> CreateFactoryAsync()
    {
        var redis = await RedisTestFixture.SharedAsync();
        return await IdentityApiFactory.CreateAsync(new Dictionary<string, string?> { ["ConnectionStrings:KnockRedis"] = redis.ConnectionString });
    }

    private static async Task<(string AccessToken, string RefreshToken, string SessionId)> SignInAsync(HttpClient client, string phone)
    {
        var (idToken, _) = await FirebaseEmulator.SignInWithPhoneAsync(phone);
        using var response = await AuthHttp.ExchangeAsync(client, idToken);
        response.EnsureSuccessStatusCode();
        var (access, refresh) = await AuthHttp.ReadSessionAsync(response);
        return (access, refresh, new JsonWebTokenHandler().ReadJsonWebToken(access).GetClaim("sid").Value);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string uri, string accessToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(uri, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    private static async Task<List<BsonDocument>> EventsAsync(IdentityApiFactory factory) =>
        await factory.Database.GetCollection<BsonDocument>("outbox").Find(Builders<BsonDocument>.Filter.Eq("tipo", _eventType)).ToListAsync();

    private static void AssertOnly(IReadOnlyList<BsonDocument> events, params string[] sessionIds)
    {
        events.Select(e => e["carga"]["sid"].AsString).Should().BeEquivalentTo(sessionIds);
        events.Should().OnlyContain(e => e["destino"].AsString == "messaging" && e["_id"].AsString == $"{_eventType}:{e["carga"]["sid"].AsString}:1");
        events.Should().OnlyContain(e => e["carga"].AsBsonDocument.Names.OrderBy(n => n).SequenceEqual(_payloadFields), "la carga lleva solo identificadores");
    }

    [Fact]
    public async Task Logout_LeavesTheSessionRevokedEventInTheSameStep_CA_037_18()
    {
        using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var (access, _, sid) = await SignInAsync(client, _phone);
        var (_, _, otherSid) = await SignInAsync(client, _phone);

        using var logout = await SendAsync(client, HttpMethod.Post, "/api/v1/auth/logout", access);
        using var again = await SendAsync(client, HttpMethod.Post, "/api/v1/auth/logout", access);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        again.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.Unauthorized);
        AssertOnly(await EventsAsync(factory), sid);
        (await factory.Sessions.Find(new BsonDocument("_id", ObjectId.Parse(otherSid))).SingleAsync())[SesionesStore.RevokedField].AsBoolean.Should().BeFalse();
    }

    [Fact]
    public async Task RemoteClose_LeavesTheEventOnlyForTheClosedSession_CA_037_18()
    {
        using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var (current, _, currentSid) = await SignInAsync(client, _phone);
        var (_, _, otherSid) = await SignInAsync(client, _phone);

        using var response = await SendAsync(client, HttpMethod.Delete, $"/api/v1/me/sesiones/{otherSid}", current);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        AssertOnly(await EventsAsync(factory), otherSid);
        (await EventsAsync(factory)).Should().NotContain(e => e["carga"]["sid"].AsString == currentSid);
    }

    [Fact]
    public async Task RemoteClose_OfAForeignOrMissingSession_LeavesNoEvent_CA_037_18()
    {
        using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var (mine, _, _) = await SignInAsync(client, _phone);
        var (_, _, foreignSid) = await SignInAsync(client, "+525555553712");

        using var foreign = await SendAsync(client, HttpMethod.Delete, $"/api/v1/me/sesiones/{foreignSid}", mine);

        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await EventsAsync(factory)).Should().BeEmpty("quien no es dueño de la sesión no provoca avisos sobre ella");
    }

    [Fact]
    public async Task EvictionByTheSessionLimit_LeavesTheEventOfTheOldestSession_CA_037_18()
    {
        using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var (_, _, oldestSid) = await SignInAsync(client, _phone);
        for (var i = 0; i < 9; i++)
        {
            await SignInAsync(client, _phone);
        }

        (await EventsAsync(factory)).Should().BeEmpty("con 10 sesiones no se expulsa a nadie");
        await SignInAsync(client, _phone);

        AssertOnly(await EventsAsync(factory), oldestSid);
        (await factory.Sessions.Find(new BsonDocument("_id", ObjectId.Parse(oldestSid))).SingleAsync())[SesionesStore.RevokedField].AsBoolean.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshTokenReuse_LeavesAnEventPerRevokedSession_CA_037_18()
    {
        using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var (_, firstRefresh, firstSid) = await SignInAsync(client, _phone);
        var (_, _, secondSid) = await SignInAsync(client, _phone);
        using var rotated = await Refresh(client, firstRefresh);
        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EventsAsync(factory)).Should().BeEmpty("renovar con un token vigente no termina ninguna sesión");

        using var reuse = await Refresh(client, firstRefresh);

        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        AssertOnly(await EventsAsync(factory), firstSid, secondSid);
        using var again = await Refresh(client, firstRefresh);
        again.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await EventsAsync(factory)).Should().HaveCount(2, "el mismo id de evento no se duplica");
    }

    private static async Task<HttpResponseMessage> Refresh(HttpClient client, string refreshToken)
    {
        using var content = JsonContent.Create(new { refreshToken });
        return await client.PostAsync(new Uri("/api/v1/auth/refresh", UriKind.Relative), content);
    }
}
