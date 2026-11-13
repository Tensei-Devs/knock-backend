extern alias GatewayApi;

using System.Net;
using System.Security.Cryptography;
using AwesomeAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Knock.SecurityTest.Gateway;

/// <summary>
/// BUG-003 (KNOCK-240) en el gateway: ningún encabezado revela la tecnología del servidor. <c>TestServer</c> no usa Kestrel y nunca escribe <c>Server</c>,
/// así que mirar solo la respuesta pasaría con o sin el arreglo: la prueba decisiva es la opción real de Kestrel (<c>AddServerHeader</c>), que es lo que
/// agrega «Server: Kestrel» al final de la respuesta en el despliegue. Solo datos ficticios.
/// </summary>
public sealed class ServerHeaderTests
{
    private static readonly string[] _leakyHeaders = ["Server", "X-Powered-By", "X-AspNet-Version"];

    private sealed class GatewayFactory : WebApplicationFactory<GatewayApi::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            builder.UseSetting("ConnectionStrings:KnockRedis", "127.0.0.1:6390,password=Kn0ck" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)));
        }
    }

    [Fact]
    public async Task Gateway_NeverSendsServer_TC_BUG_003_01()
    {
        await using var host = new GatewayFactory();
        using var client = host.CreateClient();

        host.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value.AddServerHeader
            .Should().BeFalse("Kestrel agrega «Server: Kestrel» después de los middleware; solo esta opción lo apaga");

        foreach (var path in new[] { "/health", "/api/v1/colonias/en-punto", "/interno/secreto" })
        {
            using var response = await client.GetAsync(new Uri(path, UriKind.Relative));
            response.Headers.Contains("Server").Should().BeFalse($"{path} respondió {(int)response.StatusCode}");
        }
    }

    [Fact]
    public async Task Gateway_NeverSendsOtherLeakyHeaders_TC_BUG_003_03()
    {
        await using var host = new GatewayFactory();
        using var client = host.CreateClient();

        foreach (var path in new[] { "/health", "/api/v1/colonias/en-punto", "/interno/secreto" })
        {
            using var response = await client.GetAsync(new Uri(path, UriKind.Relative));
            foreach (var header in _leakyHeaders)
            {
                response.Headers.Contains(header).Should().BeFalse($"{header} en {path} ({(HttpStatusCode)(int)response.StatusCode})");
            }
        }
    }
}
