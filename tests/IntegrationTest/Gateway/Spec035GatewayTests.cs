using System.Net.Http.Headers;
using AwesomeAssertions;
using Knock.Gateway.Hosting;
using Knock.Hosting.RateLimiting;
using Knock.IntegrationTest.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Knock.IntegrationTest.Gateway;

/// <summary>Spec 035 (KNOCK-180, R-13, CA-13): el servicio solo ve los encabezados reenviados que escribió el gateway. Solo datos ficticios.</summary>
public sealed class Spec035GatewayTests
{
    [Fact]
    public async Task Reenvia_Solo_Encabezados_Propios_CA_035_13()
    {
        await using var host = await GatewayHost.StartAsync(
            new Dictionary<string, string?> { ["ForwardedHeaders:KnownNetworks:0"] = "10.0.0.0/16" },
            services =>
            {
                services.AddSingleton<IRevokedSessions>(new NoRevokedSessions());
                services.AddSingleton<IRequestCounter>(new NoLimitCounter());
            });
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/colonias/en-punto", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GatewayTestSupport.IssueToken());
        host.Client.DefaultRequestHeaders.Remove(GatewayRemoteIpStartupFilter.HeaderName);
        request.Headers.TryAddWithoutValidation(GatewayRemoteIpStartupFilter.HeaderName, "10.0.0.5");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.7");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", "evil.example");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Prefix", "/evil");
        request.Headers.TryAddWithoutValidation("X-Original-For", "192.0.2.9");
        request.Headers.TryAddWithoutValidation("Forwarded", "for=192.0.2.9;host=evil.example");

        using var response = await host.Client.SendAsync(request);

        response.IsSuccessStatusCode.Should().BeTrue();
        var seen = host.LastHeadersSeen;
        seen.Should().NotContainKey("X-Forwarded-Host").And.NotContainKey("X-Forwarded-Prefix").And.NotContainKey("X-Original-For").And.NotContainKey("Forwarded");
        seen["X-Forwarded-For"].Should().Be("198.51.100.7", "es la IP que el gateway resolvió desde la red de confianza");
        seen["X-Forwarded-Proto"].Should().NotBeNullOrWhiteSpace();
    }
}
