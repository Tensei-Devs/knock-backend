using AwesomeAssertions;
using Knock.Identity.Api.Modules.Sesiones.Dtos;
using Knock.Identity.Api.Modules.Sesiones.Validators;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 016, R-01: forma del cuerpo de la renovación.</summary>
public class RefreshRequestValidatorTests
{
    private static readonly RefreshRequestValidator _validator = new();

    [Theory]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("x")]
    public void TokenOfAnyAcceptableLength_HasNoErrors_CA_016_01(string token)
    {
        _validator.Validate(new RefreshRequest(token)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingToken_FailsOnItsOwnField_CA_016_01(string? token)
    {
        _validator.Validate(new RefreshRequest(token)).Errors.Select(e => e.PropertyName).Should().Equal("RefreshToken");
    }

    [Fact]
    public void TokenOver128_FailsAndDoesNotRepeatTheValue_CA_016_01()
    {
        var result = _validator.Validate(new RefreshRequest(new string('z', 129)));

        result.Errors.Select(e => e.PropertyName).Should().Equal("RefreshToken");
        result.Errors.Should().OnlyContain(e => !e.ErrorMessage.Contains("zzzz", StringComparison.Ordinal));
    }
}
