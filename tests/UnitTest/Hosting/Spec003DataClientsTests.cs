extern alias gateway;
extern alias identity;
extern alias messaging;

using System.Net;
using System.Security.Cryptography;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StackExchange.Redis;
using Xunit;
using MarketHosting = Knock.Market.Api.Hosting;

namespace Knock.UnitTest.Hosting;

/// <summary>
/// Spec 003, sin contenedores: qué cliente de datos registra cada servicio (CA-02), la validación de su configuración al arrancar,
/// la readiness cuando MongoDB o Redis no responden (CA-03) y la ruta <c>/health/ready</c>. Las cadenas son sintéticas, apuntan a un
/// host que no existe y nunca deben aparecer en un mensaje.
/// </summary>
public sealed class Spec003DataClientsTests
{
    private const string _secretHost = "knock-secret-host.invalid";

    private static readonly Dictionary<string, Service> _services = new()
    {
        ["Identity"] = new(s => identity::Knock.Identity.Api.Hosting.DataExtensions.AddKnockData(s),
            e => identity::Knock.Identity.Api.Hosting.HealthExtensions.MapKnockReadiness(e), "Identity", Redis: true),
        ["Messaging"] = new(s => messaging::Knock.Messaging.Api.Hosting.DataExtensions.AddKnockData(s),
            e => messaging::Knock.Messaging.Api.Hosting.HealthExtensions.MapKnockReadiness(e), "Messaging", Redis: false),
        ["Gateway"] = new(s => gateway::Knock.Gateway.Hosting.DataExtensions.AddKnockData(s),
            e => gateway::Knock.Gateway.Hosting.HealthExtensions.MapKnockReadiness(e), MongoKey: null, Redis: true),
        // Market registra Redis a demanda del módulo que lo use (spec 003); aquí se prueba ese registro.
        ["Market"] = new(s => MarketHosting.RedisExtensions.AddKnockRedis(s), Map: null, MongoKey: null, Redis: true),
    };

    public static TheoryData<string> AllServices() => [.. _services.Keys];

    public static TheoryData<string> NewServices() => ["Identity", "Messaging", "Gateway"];

    public static TheoryData<string> RedisServices() => [.. _services.Where(s => s.Value.Redis).Select(s => s.Key)];

    public static TheoryData<string> MongoServices() => [.. _services.Where(s => s.Value.MongoKey is not null).Select(s => s.Key)];

    public static TheoryData<string, string, string?> InvalidRedisSettings()
    {
        var data = new TheoryData<string, string, string?>();
        foreach (var service in _services.Where(s => s.Value.Redis).Select(s => s.Key))
        {
            data.Add(service, "ConnectionStrings:KnockRedis", null);
            data.Add(service, "ConnectionStrings:KnockRedis", "   ");
            data.Add(service, "ConnectionStrings:KnockRedis", $"{_secretHost}:6379,password=Fake1,connectTimeout=cinco");
            foreach (var value in new[] { "-1", "16", "uno", string.Empty })
            {
                data.Add(service, "Redis:Database", value);
            }

            foreach (var key in new[] { "Redis:ConnectTimeoutSeconds", "Redis:SyncTimeoutSeconds" })
            {
                foreach (var value in new[] { "0", "61", "cinco" })
                {
                    data.Add(service, key, value);
                }
            }
        }

        return data;
    }

    public static TheoryData<string, string, string?> InvalidMongoSettings()
    {
        var data = new TheoryData<string, string, string?>();
        foreach (var (service, definition) in _services.Where(s => s.Value.MongoKey is not null))
        {
            data.Add(service, "ConnectionStrings:KnockMongo", null);
            data.Add(service, "ConnectionStrings:KnockMongo", $"http://{_secretHost}/no-es-mongo");
            data.Add(service, "Mongo:MaxPoolSize", "0");
            data.Add(service, "Mongo:MaxPoolSize", "101");
            data.Add(service, "Mongo:ServerSelectionTimeoutSeconds", "61");
            data.Add(service, "Mongo:QueryMaxTimeSeconds", "cero");
            data.Add(service, "Mongo:Region", "eastus");
            data.Add(service, $"Mongo:Databases:{definition.MongoKey}", "base con espacios");
            data.Add(service, $"Mongo:Databases:{definition.MongoKey}", null);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(NewServices))]
    public void AddKnockData_RegistersOnlyTheClientsOfEachService_Once_CA_003_02(string name)
    {
        var service = _services[name];
        var services = new ServiceCollection();

        service.Add(services);
        service.Add(services);

        services.Count(d => d.ServiceType == typeof(IMongoClient)).Should().Be(service.MongoKey is null ? 0 : 1);
        services.Count(d => d.ServiceType == typeof(IConnectionMultiplexer)).Should().Be(service.Redis ? 1 : 0);
        services.Any(d => d.ServiceType == typeof(IMongoDatabase) && Equals(d.ServiceKey, service.MongoKey))
            .Should().Be(service.MongoKey is not null);
        using var provider = services.BuildServiceProvider();
        var checks = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        checks.Select(check => check.Name).Should().BeEquivalentTo(Expected(service), "una comprobación por cliente, aunque se registre dos veces");
        checks.Should().OnlyContain(check => check.Tags.Contains("ready"));
    }

    [Theory]
    [MemberData(nameof(AllServices))]
    public void ValidConfiguration_PassesStartupValidation_AndMongoUsesTheConfiguredDatabase(string name)
    {
        var service = _services[name];
        using var provider = Build(service, ValidConfiguration(service));

        provider.GetRequiredService<IStartupValidator>().Validate();

        if (service.MongoKey is not null)
        {
            var database = provider.GetRequiredKeyedService<IMongoDatabase>(service.MongoKey);
            database.DatabaseNamespace.DatabaseName.Should().Be($"s003_{service.MongoKey}");
            var settings = provider.GetRequiredService<IMongoClient>().Settings;
            settings.MaxConnectionPoolSize.Should().Be(7, "el pool lo fija Mongo:MaxPoolSize, no la cadena");
            settings.ServerSelectionTimeout.Should().Be(TimeSpan.FromSeconds(1));
        }
    }

    [Theory]
    [MemberData(nameof(RedisServices))]
    public void RedisConnectionWithoutPassword_FailsStartupWithoutRevealingIt_CA_003_03(string name)
    {
        var service = _services[name];
        var values = ValidConfiguration(service);
        values["ConnectionStrings:KnockRedis"] = $"{_secretHost}:6379,abortConnect=false";
        using var provider = Build(service, values);

        var startup = () => provider.GetRequiredService<IStartupValidator>().Validate();

        var message = startup.Should().Throw<OptionsValidationException>().Which.Message;
        message.Should().Contain("ConnectionStrings:KnockRedis").And.Contain("password");
        message.Should().NotContain(_secretHost);
    }

    [Theory]
    [MemberData(nameof(InvalidRedisSettings))]
    public void InvalidRedisSetting_FailsStartupNamingTheKeyOnly(string name, string key, string? value) =>
        AssertStartupFails(_services[name], key, value);

    [Theory]
    [MemberData(nameof(InvalidMongoSettings))]
    public void InvalidMongoSetting_FailsStartupNamingTheKeyOnly(string name, string key, string? value) =>
        AssertStartupFails(_services[name], key, value);

    [Theory]
    [MemberData(nameof(AllServices))]
    public async Task Readiness_WithUnreachableDataStores_IsUnhealthyAndHidesTheHost_CA_003_03(string name)
    {
        var service = _services[name];
        await using var provider = Build(service, ValidConfiguration(service));

        var report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync(check => check.Tags.Contains("ready"));

        report.Entries.Keys.Should().BeEquivalentTo(Expected(service));
        foreach (var (check, entry) in report.Entries)
        {
            entry.Status.Should().Be(HealthStatus.Unhealthy, $"{check} no responde");
            entry.Description.Should().Contain("did not respond").And.NotContain(_secretHost).And.NotContain("Fake");
            entry.Exception.Should().BeNull("HealthCheckService registraría su mensaje, que puede traer el host");
        }
    }

    [Theory]
    [MemberData(nameof(NewServices))]
    public async Task MapKnockReadiness_AnswersWithOnlyTheReadyChecks(string name)
    {
        var map = _services[name].Map!;

        (await ReadyStatusAsync(map, readyHealthy: true)).Should().Be(HttpStatusCode.OK, "la comprobación sin la etiqueta ready no cuenta");
        (await ReadyStatusAsync(map, readyHealthy: false)).Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private static string[] Expected(Service service) =>
        [.. (service.MongoKey is null ? Array.Empty<string>() : ["mongodb"]), .. (service.Redis ? ["redis"] : Array.Empty<string>())];

    private static void AssertStartupFails(Service service, string key, string? value)
    {
        var values = ValidConfiguration(service);
        var secrets = values.Where(entry => entry.Key.StartsWith("ConnectionStrings:", StringComparison.Ordinal))
            .Select(entry => entry.Value!).ToList();
        values[key] = value;
        using var provider = Build(service, values);

        var startup = () => provider.GetRequiredService<IStartupValidator>().Validate();

        var message = startup.Should().Throw<OptionsValidationException>().Which.Message;
        message.Should().Contain(key);
        message.Should().NotContain(_secretHost);
        foreach (var secret in secrets)
        {
            message.Should().NotContain(secret);
        }
    }

    /// <summary>Configuración válida y sintética: hosts que no existen, contraseñas de un solo uso y tiempos de 1 s.</summary>
    private static Dictionary<string, string?> ValidConfiguration(Service service)
    {
        var password = "Fake" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
        var values = new Dictionary<string, string?>();
        if (service.MongoKey is not null)
        {
            values["ConnectionStrings:KnockMongo"] = new MongoUrlBuilder
            {
                Server = new MongoServerAddress(_secretHost, 10260),
                Username = "knock-fake",
                Password = password,
                DirectConnection = true,
            }.ToString();
            values["Mongo:MaxPoolSize"] = "7";
            values["Mongo:ServerSelectionTimeoutSeconds"] = "1";
            values["Mongo:ConnectTimeoutSeconds"] = "1";
            values[$"Mongo:Databases:{service.MongoKey}"] = $"s003_{service.MongoKey}";
        }

        if (service.Redis)
        {
            values["ConnectionStrings:KnockRedis"] = $"{_secretHost}:6379,password={password},abortConnect=false";
            values["Redis:ConnectTimeoutSeconds"] = "1";
            values["Redis:SyncTimeoutSeconds"] = "1";
        }

        return values;
    }

    private static ServiceProvider Build(Service service, Dictionary<string, string?> values)
    {
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(values).Build())
            .AddLogging();
        service.Add(services);
        return services.BuildServiceProvider();
    }

    /// <summary>Kestrel en un puerto libre de 127.0.0.1 con dos comprobaciones: una con la etiqueta ready y otra sin ella, siempre caída.</summary>
    private static async Task<HttpStatusCode> ReadyStatusAsync(Action<IEndpointRouteBuilder> map, bool readyHealthy)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHealthChecks()
            .AddCheck("lista", () => readyHealthy ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy(), tags: ["ready"])
            .AddCheck("viva", () => HealthCheckResult.Unhealthy());
        await using var app = builder.Build();
        map(app);
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
            return response.StatusCode;
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private sealed record Service(
        Action<IServiceCollection> Add, Action<IEndpointRouteBuilder>? Map, string? MongoKey, bool Redis);
}
