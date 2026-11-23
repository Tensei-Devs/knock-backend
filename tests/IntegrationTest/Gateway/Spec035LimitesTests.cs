using System.Net;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Xunit;

namespace Knock.IntegrationTest.Gateway;

/// <summary>
/// Spec 035 (KNOCK-180, R-14, CA-11): detrás de una entrada de confianza, dos clientes distintos caen en particiones distintas del límite por IP (Redis real),
/// y un <c>X-Forwarded-For</c> falso de un cliente no lo cambia de partición. Solo datos ficticios.
/// </summary>
public sealed class Spec035LimitesTests
{
    private static readonly Uri _publicUri = new("/api/v1/oficios", UriKind.Relative);

    [Fact]
    public async Task Dos_Clientes_Particiones_Distintas_CA_035_11()
    {
        var redis = await RedisTestFixture.SharedAsync();
        await using var host = await GatewayHost.StartAsync(new Dictionary<string, string?>
        {
            ["ConnectionStrings:KnockRedis"] = redis.ConnectionString,
            ["ForwardedHeaders:KnownNetworks:0"] = "10.0.0.0/16",
            ["RateLimiting:Ip:PermitLimit"] = "3",
            ["RateLimiting:Ip:Window"] = "00:01:00",
            ["RateLimiting:Auth:PermitLimit"] = "1000",
            ["RateLimiting:User:PermitLimit"] = "1000",
        });
        host.Client.DefaultRequestHeaders.Remove(GatewayRemoteIpStartupFilter.HeaderName);
        var clientA = RandomDocumentationIp();
        var clientB = $"203.0.113.{System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 254)}";

        var statusesA = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            statusesA.Add(await GetAsync(host.Client, clientA));
        }

        statusesA.Take(3).Should().OnlyContain(status => status == HttpStatusCode.OK);
        statusesA[3].Should().Be(HttpStatusCode.TooManyRequests, "el cliente A agotó su cubo");
        (await GetAsync(host.Client, clientB)).Should().Be(HttpStatusCode.OK, "el cliente B tiene su propio cubo");
        (await GetAsync(host.Client, clientA, spoofed: clientB)).Should().Be(HttpStatusCode.TooManyRequests, "con ForwardLimit = 1 el valor que escribió el cliente no lo saca de su partición");
    }

    private static string RandomDocumentationIp() => $"198.51.100.{System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 254)}";

    private static async Task<HttpStatusCode> GetAsync(HttpClient client, string forwardedFor, string? spoofed = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _publicUri);
        request.Headers.TryAddWithoutValidation(GatewayRemoteIpStartupFilter.HeaderName, "10.0.0.5");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", spoofed is null ? forwardedFor : $"{spoofed}, {forwardedFor}");
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }
}
