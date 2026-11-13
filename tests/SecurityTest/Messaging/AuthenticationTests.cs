extern alias MessagingApi;

using System.Net;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using MessagingHosting = MessagingApi::Knock.Messaging.Api.Hosting;

namespace Knock.SecurityTest.Messaging;

/// <summary>BUG-004 (KNOCK-241): Mensajería valida el JWT de Knock y la lista de sesiones canceladas como el resto de los servicios. Solo datos ficticios.</summary>
public sealed class AuthenticationTests
{
    private const string _protectedPath = "/sonda/protegida";

    /// <summary>Redis no responde: la lista de sesiones canceladas no se puede leer.</summary>
    private sealed class UnavailableRevokedSessions : MessagingHosting.IRevokedSessions
    {
        public Task<bool> IsRevokedAsync(string sessionId, CancellationToken cancellationToken) => throw new TimeoutException("synthetic");
    }

    [Fact]
    public async Task NoValidToken_Returns401_TC_BUG_004_01()
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();

        using var withoutToken = await SecureHttp.SendAsync(client, HttpMethod.Get, _protectedPath);
        withoutToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var accepted = new List<string>();
        foreach (var (name, forge) in TokenForge.Attacks)
        {
            using var response = await SecureHttp.SendAsync(client, HttpMethod.Get, _protectedPath, bearer: forge());
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                accepted.Add($"{name}:{(int)response.StatusCode}");
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync();
                body.Should().NotContainAny(SecureHttp.ForbiddenInErrors, $"el 401 de «{name}» no debe revelar el motivo");
                response.Headers.Contains("Server").Should().BeFalse();
            }
        }

        accepted.Should().BeEmpty("ningún token alterado debe autenticar");
    }

    [Fact]
    public async Task TokenWithoutSubOrSession_Returns401_TC_BUG_004_01()
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();

        using var withoutSub = await SecureHttp.SendAsync(client, HttpMethod.Get, _protectedPath, bearer: TokenForge.WithoutSub());
        using var withoutSid = await SecureHttp.SendAsync(client, HttpMethod.Get, _protectedPath, bearer: TokenForge.WithoutSid());

        withoutSub.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        withoutSid.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "sin sid no hay forma de comprobar que la sesión siga abierta");
    }

    [Fact]
    public async Task ValidToken_Passes_TC_BUG_004_02()
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();
        var sub = TokenForge.Sub();

        using var protectedResponse = await SecureHttp.SendAsync(client, HttpMethod.Get, _protectedPath, bearer: TokenForge.Valid(sub));
        using var health = await SecureHttp.SendAsync(client, HttpMethod.Get, "/health");
        using var ready = await SecureHttp.SendAsync(client, HttpMethod.Get, "/health/ready");

        protectedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await protectedResponse.Content.ReadAsStringAsync()).Should().Contain(sub, "la identidad sale del sub del token");
        health.StatusCode.Should().Be(HttpStatusCode.OK, "/health es anónimo");
        ready.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, "/health/ready es anónimo (puede responder 503 si la MongoDB de prueba no existe)");
    }

    [Fact]
    public async Task RevokedSession_Returns401_TC_BUG_004_05()
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();
        var sid = TokenForge.Sid();
        var token = TokenForge.Valid(sid: sid);

        using var before = await SecureHttp.SendAsync(client, HttpMethod.Get, _protectedPath, bearer: token);
        await host.RevokeSessionAsync(sid);
        using var after = await SecureHttp.SendAsync(client, HttpMethod.Get, _protectedPath, bearer: token);

        before.StatusCode.Should().Be(HttpStatusCode.OK);
        after.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "el logout cancela la sesión y el JWT deja de servir");
    }

    [Fact]
    public async Task RedisUnavailable_Returns503_TC_BUG_004_08()
    {
        await using var baseHost = await SecurityMessagingFactory.CreateAsync();
        await using var host = baseHost.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<MessagingHosting.IRevokedSessions>(new UnavailableRevokedSessions())));
        using var client = host.CreateClient();

        using var response = await SecureHttp.SendAsync(client, HttpMethod.Get, _protectedPath, bearer: TokenForge.Valid());
        using var withoutToken = await SecureHttp.SendAsync(client, HttpMethod.Get, _protectedPath);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, "sin Redis no se sabe si la sesión sigue viva: no se deja pasar");
        response.Headers.GetValues("Retry-After").Should().ContainSingle().Which.Should().Be("5");
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadAsStringAsync()).Should().NotContainAny(SecureHttp.ForbiddenInErrors);
        withoutToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "sin token no hay nada que comprobar en Redis: sigue siendo 401");
    }
}
