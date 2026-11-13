extern alias gateway;

using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using GatewayHosting = gateway::Knock.Gateway.Hosting;

namespace Knock.UnitTest.Gateway;

/// <summary>BUG-002 (KNOCK-239): <c>Security:Hsts</c> del gateway tiene valores por defecto documentados, es configurable y no deja arrancar con un valor inválido.</summary>
public sealed class HstsOptionsTests
{
    private static ServiceProvider Provider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration);
        GatewayHosting.HstsExtensions.AddKnockGatewayHsts(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void WithoutConfiguration_UsesTheDocumentedDefaults_TC_BUG_002_03()
    {
        using var provider = Provider([]);

        var settings = provider.GetRequiredService<IOptions<GatewayHosting.HstsSettings>>().Value;

        settings.HeaderValue.Should().Be("max-age=31536000; includeSubDomains");
        settings.HeaderValue.Should().NotContain("preload");
    }

    [Fact]
    public void MaxAge_IsConfigurable_TC_BUG_002_03()
    {
        using var provider = Provider(new() { ["Security:Hsts:MaxAge"] = "1.00:00:00", ["Security:Hsts:IncludeSubDomains"] = "false" });

        provider.GetRequiredService<IOptions<GatewayHosting.HstsSettings>>().Value.HeaderValue.Should().Be("max-age=86400");
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-1.00:00:00")]
    public void MaxAgeZeroOrNegative_IsRejectedWhenTheOptionsAreRead_TC_BUG_002_03(string maxAge)
    {
        using var provider = Provider(new() { ["Security:Hsts:MaxAge"] = maxAge });

        var act = () => provider.GetRequiredService<IOptions<GatewayHosting.HstsSettings>>().Value;

        act.Should().Throw<OptionsValidationException>().Which.Message.Should().Contain("Security:Hsts:MaxAge").And.NotContain(maxAge);
    }
}
