using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.Hosting.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Knock.UnitTest.Hosting;

/// <summary>
/// Spec 055 (T-17), autorrevisión de seguridad: el JWT propio se valida igual en los cuatro proyectos (un solo lugar) y el gateway exige la clave en todos los entornos.
/// Las claves se generan al azar en ejecución.
/// </summary>
public sealed class JwtSettingsTests
{
    private static IConfiguration Config(string? signingKey)
    {
        var values = new Dictionary<string, string?> { ["Jwt:Issuer"] = "knock-api", ["Jwt:Audience"] = "knock-app" };
        if (signingKey is not null)
        {
            values["Jwt:SigningKey"] = signingKey;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static string RandomKey(int bytes) => Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes));

    [Fact]
    public void ToValidationParameters_RequiresEverythingAndOnlyHmacSha256_TC_055_SEC_01()
    {
        var settings = JwtSettingsFactory.CreateRequiringKey(Config(RandomKey(32)));

        var parameters = settings.ToValidationParameters();

        parameters.ValidateIssuer.Should().BeTrue();
        parameters.ValidIssuer.Should().Be("knock-api");
        parameters.ValidateAudience.Should().BeTrue();
        parameters.ValidAudience.Should().Be("knock-app");
        parameters.ValidateIssuerSigningKey.Should().BeTrue();
        parameters.IssuerSigningKey.Should().BeSameAs(settings.SigningKey);
        parameters.RequireSignedTokens.Should().BeTrue();
        parameters.ValidateLifetime.Should().BeTrue();
        parameters.RequireExpirationTime.Should().BeTrue();
        parameters.ClockSkew.Should().Be(TimeSpan.Zero);
        parameters.ValidAlgorithms.Should().Equal(SecurityAlgorithms.HmacSha256);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRequiringKey_WithoutKey_FailsInEveryEnvironment_TC_055_SEC_02(string? missing)
    {
        var act = () => JwtSettingsFactory.CreateRequiringKey(Config(missing));

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("Jwt:SigningKey");
    }

    [Fact]
    public void CreateRequiringKey_ShortOrNotBase64_FailsWithoutEchoingTheValue_TC_055_SEC_03()
    {
        var shortKey = RandomKey(JwtSettingsFactory.MinimumKeyBytes - 1);
        var notBase64 = "no es base64 " + RandomKey(24);

        var tooShort = () => JwtSettingsFactory.CreateRequiringKey(Config(shortKey));
        var invalid = () => JwtSettingsFactory.CreateRequiringKey(Config(notBase64));

        tooShort.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("at least").And.NotContain(shortKey);
        invalid.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("Base64").And.NotContain(notBase64);
    }
}
