using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Knock.IntegrationTest.Support;

/// <summary>
/// El gateway real delante de tres servicios en memoria (identidad, mercado y mensajería) que responden 200 y dicen quién contestó. Las
/// dependencias de Redis se pueden sustituir por dobles o dejar las reales (con la cadena del contenedor de pruebas).
/// </summary>
internal sealed class GatewayHost : IAsyncDisposable
{
    private readonly Dictionary<string, WebApplication> _services = [];
    private readonly WebApplicationFactory<Program> _baseFactory = new();
    private WebApplicationFactory<Program>? _factory;

    public HttpClient Client { get; private set; } = null!;

    public List<string> PathsSeen { get; } = [];

    public static async Task<GatewayHost> StartAsync(
        IReadOnlyDictionary<string, string?>? settings = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var host = new GatewayHost();
        var addresses = new Dictionary<string, string>();
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
                    host.PathsSeen.Add(context.Request.Path.Value ?? string.Empty);
                    context.Response.Headers["X-Servicio"] = serviceName;
                    return Results.Ok(new { servicio = serviceName });
                });
            await app.StartAsync();
            host._services[name] = app;
            addresses[name] = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First() + "/";
        }

        host._factory = host._baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Jwt:SigningKey", GatewayTestSupport.SigningKeyBase64);
            builder.UseSetting("ConnectionStrings:KnockRedis", GatewayTestSupport.DummyRedisConnectionString);
            foreach (var (name, address) in addresses)
            {
                builder.UseSetting($"ReverseProxy:Clusters:{name}:Destinations:principal:Address", address);
            }

            foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IStartupFilter>(new GatewayRemoteIpStartupFilter());
                configureServices?.Invoke(services);
            });
        });
        host.Client = host._factory.CreateClient();
        // Cada host usa su propia IP de conexión: los contadores de Redis se comparten entre pruebas y no deben mezclarse.
        host.Client.DefaultRequestHeaders.Add(GatewayRemoteIpStartupFilter.HeaderName, new IPAddress(RandomNumberGenerator.GetBytes(4)).ToString());
        return host;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _baseFactory.DisposeAsync();
        foreach (var service in _services.Values)
        {
            await service.StopAsync();
            await service.DisposeAsync();
        }
    }
}

/// <summary>Pone la IP de conexión desde un encabezado que solo entiende el host de pruebas (TestServer no trae IP), antes del resto del pipeline.</summary>
internal sealed class GatewayRemoteIpStartupFilter : IStartupFilter
{
    public const string HeaderName = "X-Test-Remote-Ip";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(HeaderName, out var value) && IPAddress.TryParse(value.ToString(), out var address))
                {
                    context.Connection.RemoteIpAddress = address;
                }

                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
