using System.Net;
using AwesomeAssertions;
using Knock.Contracts.Correlation;
using Knock.IntegrationTest.Gateway.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Knock.IntegrationTest.Gateway;

/// <summary>
/// El gateway enruta cada petición externa al servicio correcto y la deja con <c>X-Correlation-Id</c> (spec 001, CA-02).
/// Los tres servicios son aplicaciones mínimas en memoria que dicen quién respondió y qué identificador de correlación vieron.
/// </summary>
public sealed class GatewayRoutingTests : IAsyncLifetime, IDisposable
{
    private readonly Dictionary<string, WebApplication> _services = [];
    private readonly Dictionary<string, string> _addresses = [];
    private readonly List<string?> _correlationSeen = [];
    private readonly List<(string Path, string Method, string? Authorization)> _requestsSeen = [];
    private readonly List<string> _queriesSeen = [];
    private GatewayTestHost? _host;
    private HttpClient? _client;

    public static TheoryData<string, string> MarketRoutes { get; } = new()
    {
        { "GET", "/api/v1/tecnicos/507f1f77bcf86cd799439011/servicios" },
        { "POST", "/api/v1/tecnicos/me/servicios" },
        { "PUT", "/api/v1/tecnicos/me/servicios/507f1f77bcf86cd799439011" },
        { "DELETE", "/api/v1/tecnicos/me/servicios/507f1f77bcf86cd799439011" },
        { "GET", "/api/v1/tecnicos/507f1f77bcf86cd799439011/fotos-trabajos" },
        { "GET", "/api/v1/tecnicos/me/reputacion" },
        { "GET", "/api/v1/agenda" },
        { "POST", "/api/v1/visitas/507f1f77bcf86cd799439011/reprogramar" },
        { "POST", "/api/v1/visitas/507f1f77bcf86cd799439011/reprogramar/respuesta" },
        { "POST", "/api/v1/visitas/507f1f77bcf86cd799439011/cancelar" },
        { "POST", "/api/v1/visitas/507f1f77bcf86cd799439011/cliente-ausente" },
        { "POST", "/api/v1/visitas/507f1f77bcf86cd799439011/terminar" },
        { "POST", "/api/v1/visitas/507f1f77bcf86cd799439011/no-terminado" },
        { "PUT", "/api/v1/visitas/507f1f77bcf86cd799439011/permiso-fotos" },
        { "POST", "/api/v1/visitas/507f1f77bcf86cd799439011/cotizacion-final" },
        { "POST", "/api/v1/visitas/507f1f77bcf86cd799439011/cotizacion-final/decision" },
        { "POST", "/api/v1/cotizaciones/507f1f77bcf86cd799439011/ajuste-precio" },
        { "POST", "/api/v1/ajustes/507f1f77bcf86cd799439011/decision" },
        { "POST", "/api/v1/solicitudes/507f1f77bcf86cd799439011/cancelar" },
    };

    [Fact]
    public async Task MyQuotes_UsesAuthenticatedMarketRoute_CA_048_12()
    {
        const string path = "/api/v1/tecnicos/me/cotizaciones";
        using var response = await _client!.GetAsync(new Uri(path, UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Servicio").Should().ContainSingle().Which.Should().Be("mercado");
        _requestsSeen.Should().ContainSingle().Which.Path.Should().Be(path);
    }

    public async Task InitializeAsync()
    {
        foreach (var name in new[] { "identidad", "mercado", "mensajeria" })
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var app = builder.Build();
            var serviceName = name;
            app.Map(
                "/{**ruta}",
                (HttpContext context) =>
                {
                    _correlationSeen.Add(context.Request.Headers[CorrelationId.HeaderName]);
                    _requestsSeen.Add((context.Request.Path.Value!, context.Request.Method, context.Request.Headers.Authorization.ToString()));
                    _queriesSeen.Add(context.Request.QueryString.Value ?? string.Empty);
                    context.Response.Headers["X-Servicio"] = serviceName;
                    return Results.Ok(new { servicio = serviceName });
                });
            await app.StartAsync();
            _services[name] = app;
            _addresses[name] = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First() + "/";
        }

        _host = GatewayTestHost.Start(_addresses);
        _client = _host.Client;
    }

    [Theory]
    [InlineData("/api/v1/zonas/lista-espera")]
    [InlineData("/api/v1/solicitudes/507f1f77bcf86cd799439011/cotizaciones")]
    [InlineData("/api/v1/solicitudes")]
    [InlineData("/api/v1/me/tecnicos-confianza")]
    [InlineData("/api/v1/me/tecnicos-confianza/507f1f77bcf86cd799439011")]
    [InlineData("/api/v1/busqueda")]
    [InlineData("/api/v1/solicitudes/507f1f77bcf86cd799439011")]
    [InlineData("/api/v1/solicitudes/507f1f77bcf86cd799439011/elegir")]
    [InlineData("/api/v1/visitas/507f1f77bcf86cd799439011/llegada")]
    [InlineData("/api/v1/visitas/507f1f77bcf86cd799439011/aviso")]
    public async Task ProtectedPost_IsForwardedUnchangedToMarket_CA_020_16(string path)
    {
        using var response = await _client!.PostAsync(new Uri(path, UriKind.Relative), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Servicio").Should().ContainSingle().Which.Should().Be("mercado");
        var forwarded = _requestsSeen.Should().ContainSingle().Which;
        forwarded.Path.Should().Be(path);
        forwarded.Method.Should().Be("POST");
        forwarded.Authorization.Should().Be(_client.DefaultRequestHeaders.Authorization!.ToString());
    }

    [Theory]
    [InlineData("/api/v1/zonas/lista-espera")]
    [InlineData("/api/v1/solicitudes/507f1f77bcf86cd799439011/cotizaciones")]
    [InlineData("/api/v1/solicitudes")]
    [InlineData("/api/v1/me/tecnicos-confianza")]
    [InlineData("/api/v1/me/tecnicos-confianza/507f1f77bcf86cd799439011")]
    [InlineData("/api/v1/busqueda")]
    [InlineData("/api/v1/solicitudes/507f1f77bcf86cd799439011")]
    [InlineData("/api/v1/solicitudes/507f1f77bcf86cd799439011/elegir")]
    [InlineData("/api/v1/visitas/507f1f77bcf86cd799439011/llegada")]
    [InlineData("/api/v1/visitas/507f1f77bcf86cd799439011/aviso")]
    [InlineData("/api/v1/conversaciones")]
    [InlineData("/api/v1/conversaciones/507f1f77bcf86cd799439011/mensajes")]
    [InlineData("/api/v1/conversaciones/507f1f77bcf86cd799439011/leido")]
    [InlineData("/api/v1/dispositivos")]
    public async Task ProtectedPost_WithoutToken_IsNotForwarded_CA_020_16(string path)
    {
        _client!.DefaultRequestHeaders.Authorization = null;
        using var response = await _client.PostAsync(new Uri(path, UriKind.Relative), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _requestsSeen.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(MarketRoutes))]
    public async Task MarketRoute_IsForwardedToMarketWithTheToken_KNOCK_266(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));
        using var response = await _client!.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Servicio").Should().ContainSingle().Which.Should().Be("mercado");
        var forwarded = _requestsSeen.Should().ContainSingle().Which;
        forwarded.Path.Should().Be(path);
        forwarded.Method.Should().Be(method);
        forwarded.Authorization.Should().Be(_client.DefaultRequestHeaders.Authorization!.ToString());
    }

    [Theory]
    [MemberData(nameof(MarketRoutes))]
    public async Task MarketRoute_WithoutToken_IsNotForwarded_KNOCK_266(string method, string path)
    {
        _client!.DefaultRequestHeaders.Authorization = null;
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));
        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _requestsSeen.Should().BeEmpty();
    }

    [Fact]
    public async Task PutNoticePreferences_IsRoutedToMessagingNotIdentity_PRE_02()
    {
        const string path = "/api/v1/me/preferencias-avisos";
        using var response = await _client!.PutAsync(new Uri(path, UriKind.Relative), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Servicio").Should().ContainSingle().Which.Should().Be("mensajeria");
        _requestsSeen.Should().ContainSingle().Which.Should().Match<(string Path, string Method, string? Authorization)>(r => r.Path == path && r.Method == "PUT");
    }

    [Fact]
    public async Task PutNoticePreferences_WithoutToken_IsNotForwarded_PRE_02()
    {
        _client!.DefaultRequestHeaders.Authorization = null;
        using var response = await _client.PutAsync(new Uri("/api/v1/me/preferencias-avisos", UriKind.Relative), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _requestsSeen.Should().BeEmpty();
    }

    [Fact]
    public async Task HubToken_InTheQuery_ReachesTheServiceAsABearerHeaderAndLeavesTheUrl_CA_026_18()
    {
        var token = _client!.DefaultRequestHeaders.Authorization!.Parameter!;
        _client.DefaultRequestHeaders.Authorization = null;

        using var response = await _client.PostAsync(new Uri($"/hubs/chat/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(token)}", UriKind.Relative), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Servicio").Should().ContainSingle().Which.Should().Be("mensajeria");
        _requestsSeen.Should().ContainSingle().Which.Authorization.Should().Be("Bearer " + token, "el servicio recibe el token en el encabezado");
        _queriesSeen.Should().ContainSingle().Which.Should().Be("?negotiateVersion=1", "la URL reenviada ya no lleva el token");
    }

    [Theory]
    [InlineData("/hubs/chat/negotiate")]
    [InlineData("/api/v1/me")]
    public async Task TokenInTheQuery_OnlyCountsForTheHub_AndNeedsAValidToken_CA_026_18(string path)
    {
        var token = _client!.DefaultRequestHeaders.Authorization!.Parameter!;
        _client.DefaultRequestHeaders.Authorization = null;
        var onlyForTheHub = path.StartsWith("/api", StringComparison.Ordinal);

        using var invalid = await _client.PostAsync(new Uri($"{path}?access_token=no-es-un-token", UriKind.Relative), content: null);
        using var twice = await _client.PostAsync(new Uri($"{path}?access_token={Uri.EscapeDataString(token)}&access_token={Uri.EscapeDataString(token)}", UriKind.Relative), content: null);
        using var valid = await _client.PostAsync(new Uri($"{path}?access_token={Uri.EscapeDataString(token)}", UriKind.Relative), content: null);

        invalid.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        twice.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "dos valores no se adivinan");
        valid.StatusCode.Should().Be(onlyForTheHub ? HttpStatusCode.Unauthorized : HttpStatusCode.OK, "fuera del hub el token en la URL no sirve");
        _requestsSeen.Count.Should().Be(onlyForTheHub ? 0 : 1);
    }

    public async Task DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        foreach (var service in _services.Values)
        {
            await service.StopAsync();
            await service.DisposeAsync();
        }
    }

    public void Dispose()
    {
        _host?.Dispose();
    }

    [Theory]
    [InlineData("/api/v1/auth/firebase", "identidad")]
    [InlineData("/api/v1/recuperacion-acceso", "identidad")]
    [InlineData("/api/v1/suspension/apelacion", "identidad")]
    [InlineData("/api/v1/me", "identidad")]
    [InlineData("/api/v1/me/sesiones", "identidad")]
    [InlineData("/api/v1/me/bloqueos", "identidad")]
    [InlineData("/api/v1/reportes", "identidad")]
    [InlineData("/api/v1/reportes/me", "identidad")]
    [InlineData("/api/v1/reportes/507f1f77bcf86cd799439011/apelacion", "identidad")]
    [InlineData("/api/v1/usuarios/507f1f77-bcf8-6cd7-9943-9011507f1f77/bloqueo", "identidad")]
    [InlineData("/api/v1/oficios", "mercado")]
    [InlineData("/api/v1/colonias/en-punto", "mercado")]
    [InlineData("/api/v1/zonas/cp/42800", "mercado")]
    [InlineData("/api/v1/mensajes/1", "mensajeria")]
    [InlineData("/api/v1/notificaciones", "mensajeria")]
    [InlineData("/api/v1/conversaciones", "mensajeria")]
    [InlineData("/api/v1/conversaciones/no-leidas", "mensajeria")]
    [InlineData("/api/v1/conversaciones/507f1f77bcf86cd799439011/mensajes", "mensajeria")]
    [InlineData("/api/v1/dispositivos", "mensajeria")]
    [InlineData("/api/v1/me/preferencias-avisos", "mensajeria")]
    public async Task Request_IsRoutedToTheRightService_CA_02(string path, string expectedService)
    {
        var response = await _client!.GetAsync(new Uri(path, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Servicio").Should().ContainSingle().Which.Should().Be(expectedService);
    }

    [Fact]
    public async Task Request_WithoutCorrelationId_GetsOneAndTheServiceSeesIt_CA_02()
    {
        var response = await _client!.GetAsync(new Uri("/api/v1/oficios", UriKind.Relative));

        var fromResponse = response.Headers.GetValues(CorrelationId.HeaderName).Should().ContainSingle().Subject;
        CorrelationId.IsValid(fromResponse).Should().BeTrue();
        _correlationSeen.Should().ContainSingle().Which.Should().Be(fromResponse);
    }

    [Fact]
    public async Task Request_WithAValidCorrelationId_KeepsItEndToEnd_CA_02()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/oficios", UriKind.Relative));
        request.Headers.Add(CorrelationId.HeaderName, "cliente-abc-123");

        var response = await _client!.SendAsync(request);

        response.Headers.GetValues(CorrelationId.HeaderName).Should().ContainSingle().Which.Should().Be("cliente-abc-123");
        _correlationSeen.Should().ContainSingle().Which.Should().Be("cliente-abc-123");
    }

    [Fact]
    public async Task UnknownRoute_IsNotForwardedToAnyService_R_02()
    {
        var response = await _client!.GetAsync(new Uri("/interno/secreto", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _correlationSeen.Should().BeEmpty();
    }

    [Fact]
    public async Task TheConversationPermissionRoute_IsNotPublishedInTheGateway_CA_030_17()
    {
        var path = "/api/v1/solicitudes/507f1f77bcf86cd799439011/conversacion?tecnicoId=507f1f77bcf86cd799439012";

        var response = await _client!.GetAsync(new Uri(path, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _requestsSeen.Should().BeEmpty("la ruta de permiso solo se llama por la red interna");
    }

    [Fact]
    public async Task Health_AnswersWithoutCallingAnyService_CA_01()
    {
        var response = await _client!.GetAsync(new Uri("/health", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _correlationSeen.Should().BeEmpty();
    }
}
