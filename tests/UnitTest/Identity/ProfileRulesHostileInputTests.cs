using AwesomeAssertions;
using Knock.Identity.Api.Modules.Usuarios.Dtos;
using Knock.Identity.Api.Modules.Usuarios.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 017, R-05 y R-08: entradas hostiles contra las reglas del perfil (inyección, HTML, CRLF, caracteres de control y Unicode engañoso).</summary>
public class ProfileRulesHostileInputTests
{
    private static readonly ProfileRules _rules = new(Options.Create(new ProfileOptions { HostsFoto = ["cdn.knock.test"] }));

    [Theory]
    [InlineData("Robert'); DROP TABLE usuarios;--")]
    [InlineData("{\"$gt\":\"\"}")]
    [InlineData("$where: 1")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("Ana\r\nSet-Cookie: a=b")]
    [InlineData("Ana\nPérez")]
    [InlineData("Ana\0")]
    [InlineData("Ana\a")]
    [InlineData("Ana‮Pérez")]
    [InlineData("Ana​Pérez")]
    [InlineData("Ana 123")]
    public void HostileNames_AreRejectedWithoutEchoingThem_SEC_017_21(string name)
    {
        var (update, errors) = _rules.Validate(new UpdateProfileRequest(name));

        update.Should().BeNull();
        errors.Should().ContainKey("nombre");
        errors.Values.SelectMany(v => v).Should().OnlyContain(message => !message.Contains(name.Trim(), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("12345\n67")]
    [InlineData("١٢٣٤٥")]
    [InlineData("１２３４５")]
    [InlineData("12345; DROP")]
    [InlineData("1234 5")]
    public void PostalCodesThatAreNotFiveAsciiDigits_AreRejected_SEC_017_22(string postalCode)
    {
        var (_, errors) = _rules.Validate(new UpdateProfileRequest("Ana", CodigoPostal: postalCode));

        errors.Should().ContainKey("codigoPostal");
    }

    [Theory]
    [InlineData("Centro'; DROP TABLE colonias;--")]
    [InlineData("{\"$ne\":null}")]
    [InlineData("<b>Centro</b>")]
    [InlineData("Centro\r\nX-Injected: 1")]
    [InlineData("Centro\0")]
    public void HostileColonias_AreRejectedBeforeAskingMarket_SEC_017_23(string colonia)
    {
        var (_, errors) = _rules.Validate(new UpdateProfileRequest("Ana", Colonia: colonia));

        errors.Should().ContainKey("colonia");
    }

    [Theory]
    [InlineData("https://cdn.knock.test@evil.example/a.png")]
    [InlineData("https://cdn.knock.test.evil.example/a.png")]
    [InlineData("https://evil.example/?u=cdn.knock.test")]
    [InlineData("https://cdn.knock.tеst/a.png")]
    [InlineData("//cdn.knock.test/a.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://127.0.0.1/a.png")]
    [InlineData("https://[::1]/a.png")]
    public void HostilePhotoAddresses_AreRejected_SEC_017_24(string photo)
    {
        var (_, errors) = _rules.Validate(new UpdateProfileRequest("Ana", FotoUrl: photo));

        errors.Should().ContainKey("fotoUrl");
    }

    [Fact]
    public void PhotoHostComparisonIgnoresCase_ButNeverThePort80Scheme_SEC_017_24()
    {
        _rules.Validate(new UpdateProfileRequest("Ana", FotoUrl: "https://CDN.KNOCK.TEST/a.png")).Errors.Should().BeEmpty();
        _rules.Validate(new UpdateProfileRequest("Ana", FotoUrl: "http://cdn.knock.test/a.png")).Errors.Should().ContainKey("fotoUrl");
    }

    [Fact]
    public void AMissingRequestIsAProgrammingErrorNotAValidationResult_SEC_017_25()
    {
        var act = () => _rules.Validate(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
