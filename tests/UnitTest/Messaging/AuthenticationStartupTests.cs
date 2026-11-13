extern alias messaging;

using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;
using MessagingHosting = messaging::Knock.Messaging.Api.Hosting;

namespace Knock.UnitTest.Messaging;

/// <summary>BUG-004 (KNOCK-241): Mensajería no arranca con una configuración de JWT débil o ausente, igual que Identidad y Mercado.</summary>
public sealed class AuthenticationStartupTests
{
    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Knock.Messaging.Api";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Config(IEnumerable<KeyValuePair<string, string?>> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static string Key(int bytes) => Convert.ToBase64String(new byte[bytes]);

    private static Dictionary<string, string?> Valid() =>
        new() { ["Jwt:Issuer"] = "knock-api", ["Jwt:Audience"] = "knock-app", ["Jwt:SigningKey"] = Key(32) };

    [Fact]
    public void ValidConfiguration_Loads_TC_BUG_004_06()
    {
        var settings = MessagingHosting.JwtSettingsFactory.Create(Config(Valid()), new FakeEnvironment(Environments.Production));

        settings.Issuer.Should().Be("knock-api");
        settings.Audience.Should().Be("knock-app");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-base64!!")]
    public void MissingOrInvalidKey_OutsideDevelopment_FailsFast_TC_BUG_004_06(string? signingKey)
    {
        var values = Valid();
        values["Jwt:SigningKey"] = signingKey;

        var act = () => MessagingHosting.JwtSettingsFactory.Create(Config(values), new FakeEnvironment(Environments.Production));

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("Jwt:SigningKey");
    }

    [Fact]
    public void ShortKey_FailsFast_TC_BUG_004_06()
    {
        var values = Valid();
        values["Jwt:SigningKey"] = Key(MessagingHosting.JwtSettingsFactory.MinimumKeyBytes - 1);

        var act = () => MessagingHosting.JwtSettingsFactory.Create(Config(values), new FakeEnvironment(Environments.Production));

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("at least");
    }

    [Theory]
    [InlineData("Jwt:Issuer")]
    [InlineData("Jwt:Audience")]
    public void MissingIssuerOrAudience_FailsFast_TC_BUG_004_06(string missing)
    {
        var values = Valid();
        values.Remove(missing);

        var act = () => MessagingHosting.JwtSettingsFactory.Create(Config(values), new FakeEnvironment(Environments.Production));

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain(missing);
    }

    [Fact]
    public void MissingKey_InDevelopment_GeneratesOneInsteadOfFailing_TC_BUG_004_06()
    {
        var values = Valid();
        values.Remove("Jwt:SigningKey");

        var settings = MessagingHosting.JwtSettingsFactory.Create(Config(values), new FakeEnvironment(Environments.Development));

        settings.SigningKey.Key.Length.Should().BeGreaterThanOrEqualTo(MessagingHosting.JwtSettingsFactory.MinimumKeyBytes);
    }
}
