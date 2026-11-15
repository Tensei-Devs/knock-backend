using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.SecurityTest;

/// <summary>
/// Spec 002 (estilo de la API), casos 05, 06 y 08: 422 con <c>errors</c> por campo, 400 para lo ilegible o mal formado y 409 o 422 con <c>code</c> según el estado
/// o el contenido. Hosts reales de Identidad y Mercado en Production, con MongoDB y Redis reales. Solo datos ficticios.
/// </summary>
public sealed class Spec002ApiStyleSecurityTests
{
    private const string _me = "/api/v1/me";

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var raw = await SecureHttp.AssertProblemAsync(response, expected);
        return JsonElement.Parse(raw);
    }

    [Fact]
    public async Task BadFormat_Returns422WithErrorsPerField_CA_002_05()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var person = await hosts.Identity.SignInAsync(hosts.IdentityClient);

        using var identity = await SecureHttp.SendRawJsonAsync(hosts.IdentityClient, HttpMethod.Put, _me, """{"nombre":"A"}""", bearer: person.AccessToken);
        using var market = await SecureHttp.SendAsync(hosts.MarketClient, HttpMethod.Get, "/api/v1/colonias/en-punto?lat=abc&lon=-99.1", bearer: TokenForge.Valid());

        var identityProblem = await ProblemAsync(identity, HttpStatusCode.UnprocessableEntity);
        identityProblem.GetProperty("errors").GetProperty("nombre").GetArrayLength().Should().BeGreaterThan(0);
        var marketProblem = await ProblemAsync(market, HttpStatusCode.UnprocessableEntity);
        marketProblem.GetProperty("errors").GetProperty("lat").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UnreadableBodyOrMalformedRouteId_Returns400_CA_002_06()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var person = await hosts.Identity.SignInAsync(hosts.IdentityClient);

        using var unreadable = await SecureHttp.SendRawJsonAsync(hosts.IdentityClient, HttpMethod.Put, _me, """{"nombre":""", bearer: person.AccessToken);
        using var badId = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Delete, _me + "/sesiones/no-es-un-object-id", bearer: person.AccessToken);

        await SecureHttp.AssertProblemAsync(unreadable, HttpStatusCode.BadRequest);
        await SecureHttp.AssertProblemAsync(badId, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task StateConflictIs409WithCodeAndUnprocessableContentIs422_CA_002_08()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var scheduled = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        await hosts.Identity.Users.UpdateOneAsync(new BsonDocument("sub", scheduled.Sub), Builders<BsonDocument>.Update.Set("estado", "eliminacion_programada"));
        var active = await hosts.Identity.SignInAsync(hosts.IdentityClient);

        using var conflict = await SecureHttp.SendRawJsonAsync(hosts.IdentityClient, HttpMethod.Put, _me, """{"nombre":"Ana"}""", bearer: scheduled.AccessToken);
        using var unprocessable = await SecureHttp.SendRawJsonAsync(hosts.IdentityClient, HttpMethod.Put, _me, """{"nombre":"Ana","colonia":"Inventada"}""", bearer: active.AccessToken);

        (await ProblemAsync(conflict, HttpStatusCode.Conflict)).GetProperty("code").GetString().Should().Be("deletion_scheduled");
        (await ProblemAsync(unprocessable, HttpStatusCode.UnprocessableEntity)).GetProperty("code").GetString().Should().Be("colonia_not_found");
    }
}
