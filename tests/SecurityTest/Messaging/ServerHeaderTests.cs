extern alias MessagingApi;

using AwesomeAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Knock.SecurityTest.Messaging;

/// <summary>
/// BUG-003 (KNOCK-240) en Mensajería: no se revela «Server: Kestrel». <c>TestServer</c> nunca escribe ese encabezado, así que además de mirar la
/// respuesta se comprueba la opción real de Kestrel (<c>AddServerHeader</c>). Solo datos ficticios.
/// </summary>
public sealed class ServerHeaderTests
{
    private sealed class MessagingFactory : WebApplicationFactory<MessagingApi::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.UseSetting("ConnectionStrings:KnockMongo", "mongodb://127.0.0.1:27099/?serverSelectionTimeoutMS=500");
        }
    }

    [Fact]
    public async Task Messaging_NeverSendsServer_TC_BUG_003_02()
    {
        await using var host = new MessagingFactory();
        using var client = host.CreateClient();

        host.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value.AddServerHeader
            .Should().BeFalse("Kestrel agrega «Server: Kestrel» después de los middleware; solo esta opción lo apaga");

        foreach (var path in new[] { "/health", "/interno/secreto" })
        {
            using var response = await client.GetAsync(new Uri(path, UriKind.Relative));
            response.Headers.Contains("Server").Should().BeFalse($"{path} respondió {(int)response.StatusCode}");
            response.Headers.Contains("X-Powered-By").Should().BeFalse();
        }
    }
}
