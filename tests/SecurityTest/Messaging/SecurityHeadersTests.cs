using System.Net;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using Xunit;

namespace Knock.SecurityTest.Messaging;

/// <summary>BUG-004 (KNOCK-241): toda respuesta de Mensajería, también las de error, lleva los encabezados de seguridad y ninguno que revele el servidor. Solo datos ficticios.</summary>
public sealed class SecurityHeadersTests
{
    [Fact]
    public async Task EveryResponse_CarriesTheHeaders_TC_BUG_004_03()
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();

        var responses = new List<HttpResponseMessage>
        {
            await SecureHttp.SendAsync(client, HttpMethod.Get, "/health"),
            await SecureHttp.SendAsync(client, HttpMethod.Get, "/sonda/protegida", bearer: TokenForge.Valid()),
            await SecureHttp.SendAsync(client, HttpMethod.Get, "/sonda/protegida"),
            await SecureHttp.SendAsync(client, HttpMethod.Get, "/ruta/que/no/existe"),
            await SecureHttp.SendAsync(client, HttpMethod.Get, "/ruta/que/no/existe", bearer: TokenForge.Valid()),
        };

        try
        {
            responses.Select(r => r.StatusCode).Should().Contain([HttpStatusCode.OK, HttpStatusCode.Unauthorized]);
            foreach (var response in responses)
            {
                var label = $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} -> {(int)response.StatusCode}";
                response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle(label).Which.Should().Be("nosniff");
                response.Headers.GetValues("X-Frame-Options").Should().ContainSingle(label).Which.Should().Be("DENY");
                response.Headers.GetValues("Referrer-Policy").Should().ContainSingle(label).Which.Should().Be("no-referrer");
                response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle(label);
                response.Headers.GetValues("Cache-Control").Should().ContainSingle(label).Which.Should().Be("no-store");
                foreach (var leaky in SecureHttp.LeakyHeaders)
                {
                    response.Headers.Contains(leaky).Should().BeFalse($"{leaky} en {label}");
                }
            }
        }
        finally
        {
            responses.ForEach(r => r.Dispose());
        }
    }
}
