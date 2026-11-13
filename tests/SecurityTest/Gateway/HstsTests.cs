extern alias GatewayApi;

using System.Net;
using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using GatewayHosting = GatewayApi::Knock.Gateway.Hosting;

namespace Knock.SecurityTest.Gateway;

/// <summary>
/// BUG-002 (KNOCK-239): el gateway pide HSTS en toda respuesta fuera de Development, sin depender de que Kestrel vea HTTPS. Detrás del ingreso la petición llega
/// como HTTP y <c>UseHsts</c> no emitía nada. Solo datos ficticios.
/// </summary>
public sealed class HstsTests
{
    private const string _expected = "max-age=31536000; includeSubDomains";
    private const string _trusted = "10.0.0.5";
    private const string _untrusted = "203.0.113.9";

    private static readonly string[] _paths = ["/health", "/api/v1/colonias/en-punto", "/interno/secreto"];

    /// <summary>Un contador de peticiones de mentira: el gateway cuenta contra Redis, y estas pruebas no usan Redis.</summary>
    private sealed class FakeCounter(long count) : GatewayHosting.IRequestCounter
    {
        public Task<(long Count, TimeSpan Remaining)> HitAsync(string key, TimeSpan window, CancellationToken cancellationToken) =>
            Task.FromResult((count, TimeSpan.FromSeconds(30)));
    }

    private sealed class GatewayHstsFactory(string environment, IReadOnlyDictionary<string, string?> settings, long counted) : WebApplicationFactory<GatewayApi::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.UseEnvironment(environment);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            builder.UseSetting("ConnectionStrings:KnockRedis", "127.0.0.1:6390,password=Kn0ck" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)));
            builder.UseSetting("ForwardedHeaders:KnownNetworks:0", "10.0.0.0/8");
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IStartupFilter>(new RemoteIpStartupFilter());
                services.AddSingleton<GatewayHosting.IRequestCounter>(new FakeCounter(counted));
            });
        }
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string remoteIp, string? forwardedProto)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        request.Headers.Add(SecurityIdentityFactory.RemoteIpHeader, remoteIp);
        if (forwardedProto is not null)
        {
            request.Headers.Add("X-Forwarded-Proto", forwardedProto);
        }

        return await client.SendAsync(request);
    }

    private static readonly Dictionary<string, string?> _noSettings = [];

    [Fact]
    public async Task ForwardedHttps_ResponseCarriesHsts_TC_BUG_002_01()
    {
        await using var open = new GatewayHstsFactory("Production", _noSettings, counted: 1);
        await using var limited = new GatewayHstsFactory("Production", _noSettings, counted: 100_000);
        using var openClient = open.CreateClient();
        using var limitedClient = limited.CreateClient();

        var statuses = new List<HttpStatusCode>();
        foreach (var path in _paths)
        {
            using var response = await GetAsync(openClient, path, _trusted, "https");
            statuses.Add(response.StatusCode);
            response.Headers.GetValues("Strict-Transport-Security").Should().ContainSingle($"{path} → {(int)response.StatusCode}").Which.Should().Be(_expected);
        }

        using var tooMany = await GetAsync(limitedClient, "/api/v1/oficios", _trusted, "https");
        statuses.Add(tooMany.StatusCode);
        tooMany.Headers.GetValues("Strict-Transport-Security").Should().ContainSingle("también en el 429").Which.Should().Be(_expected);

        statuses.Should().Contain([HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], "se probó en 200, 401 y 429");
    }

    [Fact]
    public async Task UntrustedSource_StillCarriesHsts_TC_BUG_002_02()
    {
        await using var host = new GatewayHstsFactory("Production", _noSettings, counted: 1);
        using var client = host.CreateClient();

        using var withoutProto = await GetAsync(client, "/health", _trusted, forwardedProto: null);
        using var untrustedSource = await GetAsync(client, "/health", _untrusted, "https");
        using var untrustedHttp = await GetAsync(client, "/health", _untrusted, "http");

        foreach (var response in new[] { withoutProto, untrustedSource, untrustedHttp })
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.GetValues("Strict-Transport-Security").Should().ContainSingle().Which.Should().Be(_expected);
        }
    }

    [Fact]
    public async Task DefaultAndConfiguredValues_NeverPreload_TC_BUG_002_03()
    {
        await using var defaults = new GatewayHstsFactory("Production", _noSettings, counted: 1);
        await using var configured = new GatewayHstsFactory("Production", new Dictionary<string, string?> { ["Security:Hsts:MaxAge"] = "00:10:00", ["Security:Hsts:IncludeSubDomains"] = "false" }, counted: 1);
        using var defaultsClient = defaults.CreateClient();
        using var configuredClient = configured.CreateClient();

        using var byDefault = await GetAsync(defaultsClient, "/health", _trusted, "https");
        using var custom = await GetAsync(configuredClient, "/health", _trusted, "https");

        byDefault.Headers.GetValues("Strict-Transport-Security").Single().Should().Be(_expected).And.NotContain("preload");
        custom.Headers.GetValues("Strict-Transport-Security").Single().Should().Be("max-age=600").And.NotContain("preload");
    }

    [Fact]
    public async Task Development_DoesNotSendHsts_TC_BUG_002_04()
    {
        await using var host = new GatewayHstsFactory("Development", _noSettings, counted: 1);
        using var client = host.CreateClient();

        using var response = await GetAsync(client, "/health", _trusted, "https");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Strict-Transport-Security").Should().BeFalse("fijaría HSTS en el localhost de quien desarrolla");
    }
}
