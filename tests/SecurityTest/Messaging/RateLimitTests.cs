using System.Globalization;
using System.Net;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using Xunit;

namespace Knock.SecurityTest.Messaging;

/// <summary>BUG-004 (KNOCK-241): Mensajería limita las peticiones por IP con 429, <c>Retry-After</c> y un cuerpo uniforme. Solo datos ficticios.</summary>
public sealed class RateLimitTests
{
    private const int _limit = 3;

    [Fact]
    public async Task OverTheLimit_Returns429_TC_BUG_004_04()
    {
        await using var host = await SecurityMessagingFactory.CreateAsync(new Dictionary<string, string?>
        {
            ["RateLimiting:Global:PermitLimit"] = _limit.ToString(CultureInfo.InvariantCulture),
            ["RateLimiting:Global:Window"] = "00:10:00",
        });
        using var client = host.CreateClient();
        const string attacker = "203.0.113.10";
        const string other = "203.0.113.20";
        var token = TokenForge.Valid();

        for (var i = 0; i < _limit; i++)
        {
            using var allowed = await SecureHttp.SendAsync(client, HttpMethod.Get, "/sonda/protegida", bearer: token, remoteIp: attacker);
            allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var limited = await SecureHttp.SendAsync(client, HttpMethod.Get, "/sonda/protegida", bearer: token, remoteIp: attacker);
        using var unaffected = await SecureHttp.SendAsync(client, HttpMethod.Get, "/sonda/protegida", bearer: token, remoteIp: other);
        using var health = await SecureHttp.SendAsync(client, HttpMethod.Get, "/health", remoteIp: attacker);

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.GetValues("Retry-After").Should().ContainSingle().Which.Should().MatchRegex("^[1-9][0-9]*$");
        limited.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        (await limited.Content.ReadAsStringAsync()).Should().NotContainAny(attacker, "RateLimiting", "global:");
        unaffected.StatusCode.Should().Be(HttpStatusCode.OK, "el límite es por IP");
        health.StatusCode.Should().Be(HttpStatusCode.OK, "/health no se limita");
    }
}
