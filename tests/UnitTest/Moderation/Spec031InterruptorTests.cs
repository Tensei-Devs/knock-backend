using AwesomeAssertions;
using Azure.Core;
using Knock.Contracts.Moderation;
using Knock.Market.Api.Modules.Moderation;
using Knock.Market.Api.Modules.Moderation.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Knock.UnitTest.Moderation;

/// <summary>Spec 031, R-15: el cableado de Content Safety (endpoint y credencial administrada) solo existe con <c>Moderacion:Habilitado=true</c>; apagado es el comportamiento de siempre.</summary>
public sealed class Spec031InterruptorTests
{
    private static ServiceCollection Build(Dictionary<string, string?> values, out IConfiguration configuration)
    {
        configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(new FakeEnvironment());
        services.AddModerationModule(configuration);
        return services;
    }

    [Fact]
    public void Switch_Off_Keeps_Local_Moderation_And_No_Azure_Credential_CA_031_15()
    {
        var services = Build([], out _);

        services.Should().NotContain(d => d.ServiceType == typeof(TokenCredential));
        services.Should().NotContain(d => d.ServiceType == typeof(IContentSafetyClient));
        using var provider = services.BuildServiceProvider();
        provider.GetService<IContentSafetyClient>().Should().BeNull();
        provider.GetRequiredService<IContentModeration>().Should().NotBeNull();
    }

    [Fact]
    public void Switch_On_Registers_Managed_Identity_Credential_And_Client_With_Endpoint_CA_031_15()
    {
        var services = Build(new() { ["Moderacion:Habilitado"] = "true", ["Moderacion:ContentSafety:Endpoint"] = "https://cs.example.test/" }, out _);

        services.Should().Contain(d => d.ServiceType == typeof(TokenCredential));
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TokenCredential>().Should().NotBeNull();
        provider.GetRequiredService<IContentSafetyClient>().Should().BeOfType<ContentSafetyClient>();
    }

    [Fact]
    public void Switch_On_Without_Endpoint_Fails_Validation_CA_031_15()
    {
        var result = new ModerationOptionsValidator(new FakeEnvironment()).Validate(null, new ModerationOptions { Habilitado = true });

        result.Failed.Should().BeTrue();
    }

    private sealed class FakeEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";

        public string ApplicationName { get; set; } = "test";

        public string ContentRootPath { get; set; } = ".";

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
