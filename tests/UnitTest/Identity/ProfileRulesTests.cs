using AwesomeAssertions;
using Knock.Identity.Api.Modules.Usuarios.Dtos;
using Knock.Identity.Api.Modules.Usuarios.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 017, R-05 y CA-06: reglas de los cuatro campos editables del perfil.</summary>
public class ProfileRulesTests
{
    private static readonly ProfileRules _rules = new(Options.Create(new ProfileOptions { HostsFoto = ["cdn.knock.test", "stknock.blob.core.windows.net"] }));

    private static HashSet<string> FieldsWithErrors(UpdateProfileRequest request) => [.. _rules.Validate(request).Errors.Keys];

    [Theory]
    [InlineData("Ana")]
    [InlineData("José Ángel")]
    [InlineData("María-José")]
    [InlineData("O'Connor")]
    [InlineData("Nuñez Peña")]
    [InlineData("Li")]
    public void ValidNames_AreAccepted_CA_017_05(string name)
    {
        var (update, errors) = _rules.Validate(new UpdateProfileRequest(name));

        errors.Should().BeEmpty();
        update.Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    [InlineData(" A ")]
    [InlineData("Ana123")]
    [InlineData("Ana_Perez")]
    [InlineData("-Ana")]
    [InlineData("<script>")]
    public void InvalidNames_AreRejected_CA_017_06(string? name)
    {
        FieldsWithErrors(new UpdateProfileRequest(name)).Should().BeEquivalentTo("nombre");
    }

    [Fact]
    public void NameOver80Characters_IsRejected_CA_017_06()
    {
        FieldsWithErrors(new UpdateProfileRequest(new string('a', 81))).Should().BeEquivalentTo("nombre");
        _rules.Validate(new UpdateProfileRequest(new string('a', 80))).Errors.Should().BeEmpty();
    }

    [Fact]
    public void NameIsTrimmed_CA_017_05()
    {
        _rules.Validate(new UpdateProfileRequest("  Ana  ")).Update!.Name.Should().Be("Ana");
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("123456")]
    [InlineData("12 45")]
    [InlineData("abcde")]
    [InlineData("１２３４５")]
    public void InvalidPostalCodes_AreRejected_CA_017_06(string postalCode)
    {
        FieldsWithErrors(new UpdateProfileRequest("Ana", CodigoPostal: postalCode)).Should().BeEquivalentTo("codigoPostal");
    }

    [Fact]
    public void FivePostalDigits_AreAccepted_CA_017_05()
    {
        _rules.Validate(new UpdateProfileRequest("Ana", CodigoPostal: "42800")).Update!.PostalCode.Should().Be("42800");
    }

    [Theory]
    [InlineData("https://cdn.knock.test/fotos/a.jpg")]
    [InlineData("https://stknock.blob.core.windows.net/imagenes/a.png")]
    [InlineData("https://CDN.KNOCK.TEST/a.jpg")]
    public void PhotosFromTheKnockStorageOrCdn_AreAccepted_CA_017_05(string photo)
    {
        _rules.Validate(new UpdateProfileRequest("Ana", FotoUrl: photo)).Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://ejemplo.com/a.jpg")]
    [InlineData("http://cdn.knock.test/a.jpg")]
    [InlineData("https://cdn.knock.test.malicioso.example/a.jpg")]
    [InlineData("https://usuario:clave@cdn.knock.test/a.jpg")]
    [InlineData("//cdn.knock.test/a.jpg")]
    [InlineData("javascript:alert(1)")]
    [InlineData("no es una dirección")]
    public void PhotosFromAnotherDomainOrScheme_AreRejected_CA_017_06(string photo)
    {
        FieldsWithErrors(new UpdateProfileRequest("Ana", FotoUrl: photo)).Should().BeEquivalentTo("fotoUrl");
    }

    [Fact]
    public void WithoutConfiguredHosts_NoPhotoIsAccepted_CA_017_06()
    {
        var rules = new ProfileRules(Options.Create(new ProfileOptions()));

        rules.Validate(new UpdateProfileRequest("Ana", FotoUrl: "https://cdn.knock.test/a.jpg")).Errors.Keys.Should().BeEquivalentTo("fotoUrl");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOptionals_AreStoredAsNoValue_CA_017_05(string? empty)
    {
        var update = _rules.Validate(new UpdateProfileRequest("Ana", empty, empty, empty)).Update!;

        update.PhotoUrl.Should().BeNull();
        update.Colonia.Should().BeNull();
        update.PostalCode.Should().BeNull();
    }

    [Theory]
    [InlineData("Centro")]
    [InlineData("San Marcos (Norte)")]
    [InlineData("Col. Jardines del Sur #2")]
    public void PlausibleColoniaNames_PassTheShapeCheck_CA_017_05(string colonia)
    {
        _rules.Validate(new UpdateProfileRequest("Ana", Colonia: colonia)).Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("<script>")]
    [InlineData("'; drop table")]
    [InlineData("{\"$ne\": null}")]
    public void ColoniaWithOddCharacters_IsRejectedBeforeAskingMarket_CA_017_06(string colonia)
    {
        FieldsWithErrors(new UpdateProfileRequest("Ana", Colonia: colonia)).Should().BeEquivalentTo("colonia");
    }

    [Fact]
    public void ErrorMessages_NeverRepeatTheSentValue_CA_017_10()
    {
        var errors = _rules.Validate(new UpdateProfileRequest("Ana123", "https://ejemplo.com/secreto.jpg", "<script>", "99")).Errors;

        errors.Values.SelectMany(messages => messages).Should().NotContain(message =>
            message.Contains("Ana123", StringComparison.Ordinal) || message.Contains("secreto", StringComparison.Ordinal) || message.Contains("script", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryInvalidField_IsReportedAtOnce_CA_017_06()
    {
        FieldsWithErrors(new UpdateProfileRequest("A", "https://ejemplo.com/a.jpg", "<x>", "1")).Should().BeEquivalentTo("nombre", "fotoUrl", "colonia", "codigoPostal");
    }

    [Theory]
    [InlineData("https://cdn.knock.test/a.png\r\nX-Injected: 1")]
    [InlineData("https://cdn.knock.test/a\n.png")]
    [InlineData("https://cdn.knock.test/a\t.png")]
    [InlineData("https://cdn.knock.test/a .png")]
    [InlineData("https://cdn.knock.test/a\u0000.png")]
    public void PhotoWithControlCharactersOrSpaces_IsRejected_CA_017_06(string photo)
    {
        FieldsWithErrors(new UpdateProfileRequest("Ana", FotoUrl: photo)).Should().Contain("fotoUrl", "no se guarda una dirección con saltos de línea (KNOCK-203, H-3)");
    }
}
