using System.Net.Http.Headers;
using Knock.Gateway.Hosting;
using Knock.IntegrationTest.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Knock.IntegrationTest.Gateway.Support;

/// <summary>
/// El gateway real de las pruebas de enrutamiento y correlación: crea la fábrica, sustituye Redis por
/// dobles, fija los destinos de YARP a las direcciones indicadas y entrega el cliente ya autenticado.
/// Los servicios de prueba que atienden las peticiones los levanta y los destruye cada prueba.
/// </summary>
internal sealed class GatewayTestHost : IDisposable, IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _baseFactory;
    private readonly WebApplicationFactory<Program> _factory;

    private GatewayTestHost(WebApplicationFactory<Program> baseFactory, WebApplicationFactory<Program> factory, HttpClient client)
    {
        _baseFactory = baseFactory;
        _factory = factory;
        Client = client;
    }

    public HttpClient Client { get; }

    public static GatewayTestHost Start(IReadOnlyDictionary<string, string> destinations, Action<IServiceCollection>? configureServices = null)
    {
        var baseFactory = new WebApplicationFactory<Program>();
        var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            // El gateway exige sesión (spec 009): la clave y Redis son sintéticos y Redis se sustituye por
            // dobles, porque estas pruebas solo cubren el enrutamiento y la correlación.
            builder.UseSetting("Jwt:SigningKey", GatewayTestSupport.SigningKeyBase64);
            builder.UseSetting("ConnectionStrings:KnockRedis", GatewayTestSupport.DummyRedisConnectionString);
            foreach (var (name, address) in destinations)
            {
                builder.UseSetting($"ReverseProxy:Clusters:{name}:Destinations:principal:Address", address);
            }

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IRevokedSessions>(new NoRevokedSessions());
                services.AddSingleton<IRequestCounter>(new NoLimitCounter());
                configureServices?.Invoke(services);
            });
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GatewayTestSupport.IssueToken());
        return new GatewayTestHost(baseFactory, factory, client);
    }

    public void Dispose()
    {
        Client.Dispose();
        _factory.Dispose();
        _baseFactory.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
        await _baseFactory.DisposeAsync();
    }
}
