extern alias IdentityApi;
extern alias MarketApi;
extern alias MessagingApi;

using System.Security.Cryptography;
using IdentityApi::Knock.Identity.Api.Modules.Sesiones.Services;
using MarketApi::Knock.Market.Api.Modules.Requests.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Knock.ContractTest.Support;

/// <summary>
/// Fábricas por servicio (spec 034, R-16): arrancan cada API en Development con configuración sintética y destinos que no responden
/// (igual que <c>scripts/export_openapi.py</c>), de modo que se pueda leer sus rutas reales sin tocar MongoDB ni Redis. Los secretos se
/// generan al azar en cada proceso y no se escriben en disco.
/// </summary>
internal static class ServiceContractHosts
{
    private const string _unreachableMongo = "mongodb://127.0.0.1:9/?directConnection=true";

    public static WebApplicationFactory<LoginService> Identity() => new IdentityHost(Settings(new()
    {
        ["Firebase:ProjectId"] = "demo-knock",
        ["Identity:PhoneProtection:EncryptionKeys:1"] = RandomKey(),
        ["Identity:PhoneProtection:HmacKeys:1"] = RandomKey(),
        ["Identity:PhoneProtection:EncryptionKeyVersion"] = "1",
        ["Identity:PhoneProtection:HmacKeyVersion"] = "1",
    }));

    public static WebApplicationFactory<RequestsApi> Market() => new MarketHost(Settings(new()
    {
        ["Busqueda:Cursor:Key"] = RandomKey(),
    }));

    public static WebApplicationFactory<MessagingApi::Program> Messaging() => new MessagingHost(Settings(new()));

    private static Dictionary<string, string?> Settings(Dictionary<string, string?> extra)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:KnockMongo"] = _unreachableMongo,
            ["ConnectionStrings:KnockRedis"] = "127.0.0.1:6379,password=" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)),
            ["Jwt:SigningKey"] = RandomKey(),
        };
        foreach (var (key, value) in extra)
        {
            values[key] = value;
        }

        return values;
    }

    private static string RandomKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static void Configure(IWebHostBuilder builder, Dictionary<string, string?> values)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
    }

    private sealed class IdentityHost(Dictionary<string, string?> values) : WebApplicationFactory<LoginService>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => Configure(builder, values);
    }

    private sealed class MarketHost(Dictionary<string, string?> values) : WebApplicationFactory<RequestsApi>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => Configure(builder, values);
    }

    private sealed class MessagingHost(Dictionary<string, string?> values) : WebApplicationFactory<MessagingApi::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => Configure(builder, values);
    }
}
