using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Knock.Market.Api.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

namespace Knock.DatabaseTest.Redis;

/// <summary>
/// Spec 003, CA-03 (R-07): la prueba de integración usa Redis real en contenedor, con contraseña y sin persistencia, a través del
/// registro de la API (<c>AddKnockRedis</c>). Solo datos ficticios.
/// </summary>
public sealed class Spec003RedisTests
{
    [Fact]
    public async Task AddKnockRedis_AgainstRealContainer_IsHealthyAndRoundTrips_CA_003_03()
    {
        var redis = await RedisFixture.SharedAsync();
        await using var provider = Build(redis.ConnectionString);

        _ = provider.GetRequiredService<IOptions<KnockRedisOptions>>().Value;
        var health = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();
        var database = provider.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var key = "s003:ficticio:" + Guid.NewGuid().ToString("N");
        await database.StringSetAsync(key, "valor-ficticio", TimeSpan.FromMinutes(1));

        health.Entries[RedisExtensions.HealthCheckName].Status.Should().Be(HealthStatus.Healthy);
        ((string?)await database.StringGetAsync(key)).Should().Be("valor-ficticio");
    }

    [Fact]
    public async Task RealContainer_RequiresPassword_AndHasNoPersistence_CA_003_03()
    {
        var redis = await RedisFixture.SharedAsync();

        var anonymous = await redis.RunCliAsync("ping");
        var save = await redis.RunAuthenticatedCliAsync("config", "get", "save");
        var appendOnly = await redis.RunAuthenticatedCliAsync("config", "get", "appendonly");

        anonymous.Output.Should().Contain("NOAUTH", "sin contraseña el servidor rechaza los comandos");
        save.Output.Split('\n', StringSplitOptions.TrimEntries).Take(2).Should().Equal(["save", string.Empty], "sin instantáneas RDB");
        appendOnly.Output.Split('\n', StringSplitOptions.TrimEntries).Take(2).Should().Equal(["appendonly", "no"], "sin AOF");
    }

    [Fact]
    public async Task ConnectionStringWithoutPassword_FailsStartupWithoutRevealingIt_CA_003_03()
    {
        var redis = await RedisFixture.SharedAsync();
        var withoutPassword = string.Join(',', redis.ConnectionString.Split(',').Where(part => !part.StartsWith("password=", StringComparison.Ordinal)));
        await using var provider = Build(withoutPassword);

        var startup = () => provider.GetRequiredService<IOptions<KnockRedisOptions>>().Value;

        var message = startup.Should().Throw<OptionsValidationException>().Which.Message;
        message.Should().Contain(KnockRedisOptions.ConnectionStringKey).And.NotContain(withoutPassword);
    }

    private static ServiceProvider Build(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [KnockRedisOptions.ConnectionStringKey] = connectionString })
            .Build();
        var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration).AddLogging();
        services.AddKnockRedis();
        return services.BuildServiceProvider();
    }
}
