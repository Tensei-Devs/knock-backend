using System.Net;
using System.Security.Cryptography;
using Knock.ContractTest.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Knock.IntegrationTest.Hosting;

/// <summary>
/// Arranca cada uno de los cuatro proyectos tal como es, sin base ni contenedores (spec 055, CA-04 y CA-05). Identidad, Mercado y Mensajería usan las
/// fábricas de <c>ContractTest</c> (<see cref="ServiceContractHosts"/>); el gateway, la suya. Los secretos se generan al azar y no se escriben en disco.
/// </summary>
internal static class Spec055Hosts
{
    public static readonly string[] Projects = ["gateway", "identity", "messaging", "market"];

    /// <summary>El host del proyecto con <paramref name="settings"/> añadidos; quien llama lo libera.</summary>
    public static Spec055Host Start(string project, IReadOnlyDictionary<string, string> settings, IPAddress? connectionIp = null)
    {
        var observed = new ObservedConnection(connectionIp);
        return project switch
        {
            "gateway" => Wrap(() => new GatewayFactory(), settings, observed),
            "identity" => Wrap(ServiceContractHosts.Identity, settings, observed),
            "messaging" => Wrap(ServiceContractHosts.Messaging, settings, observed),
            "market" => Wrap(ServiceContractHosts.Market, settings, observed),
            _ => throw new ArgumentOutOfRangeException(nameof(project), project, "Proyecto desconocido."),
        };
    }

    private static Spec055Host Wrap<T>(Func<WebApplicationFactory<T>> create, IReadOnlyDictionary<string, string> settings, ObservedConnection observed)
        where T : class
    {
        var factory = create();
        var configured = factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter>(observed));
        });
        return new Spec055Host(factory, configured.Server.Services, configured.CreateClient, observed);
    }

    /// <summary>Fija la IP de la conexión (TestServer no trae ninguna) antes del pipeline y anota la que queda después de él.</summary>
    internal sealed class ObservedConnection(IPAddress? connectionIp) : IStartupFilter
    {
        public IPAddress? Resolved { get; private set; }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, following) =>
            {
                context.Connection.RemoteIpAddress = connectionIp;
                await following(context);
                Resolved = context.Connection.RemoteIpAddress;
            });
            next(app);
        };
    }

    private sealed class GatewayFactory : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.UseEnvironment("Development");
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            builder.UseSetting("ConnectionStrings:KnockRedis", "127.0.0.1:6390,password=" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));
        }
    }
}

/// <summary>Un host arrancado de <see cref="Spec055Hosts"/>.</summary>
internal sealed class Spec055Host(IAsyncDisposable owner, IServiceProvider services, Func<HttpClient> createClient, Spec055Hosts.ObservedConnection observed) : IAsyncDisposable
{
    public IServiceProvider Services => services;

    public IPAddress? ResolvedIp => observed.Resolved;

    public HttpClient CreateClient() => createClient();

    public ValueTask DisposeAsync() => owner.DisposeAsync();
}
