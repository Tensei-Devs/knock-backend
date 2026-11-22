using AwesomeAssertions;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using Knock.Market.Api.Modules.Tecnicos.Validators;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Tecnicos;

/// <summary>Spec 047 (SRV-01 a SRV-04): validadores de forma del servicio y de las rutas. Solo datos ficticios.</summary>
public sealed class Spec047ServiciosTests
{
    private static CreateServiceBody Valid() => new("Cambio de llave", "Incluye materiales", 350, "plo");

    [Fact]
    public void Create_Valid_Passes_CA_047_06() =>
        new CreateServiceBodyValidator().Validate(Valid()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NameMissingOrBlank_Fails_CA_047_08(string? name) =>
        new CreateServiceBodyValidator().Validate(Valid() with { Nombre = name }).IsValid.Should().BeFalse();

    [Fact]
    public void Create_NameOfSixtyOneCharacters_Fails_CA_047_08() =>
        new CreateServiceBodyValidator().Validate(Valid() with { Nombre = new string('a', 61) }).IsValid.Should().BeFalse();

    [Fact]
    public void Create_NameOfSixtyCharacters_Passes_CA_047_08() =>
        new CreateServiceBodyValidator().Validate(Valid() with { Nombre = new string('a', 60) }).IsValid.Should().BeTrue();

    [Fact]
    public void Create_DetailOfOneHundredFiftyOneCharacters_Fails_CA_047_08() =>
        new CreateServiceBodyValidator().Validate(Valid() with { Detalle = new string('a', 151) }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_PriceMissingOrNotPositive_Fails_CA_047_08(int? price) =>
        new CreateServiceBodyValidator().Validate(Valid() with { PrecioReferencia = price }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Plo mero")]
    [InlineData("0123456789abcdefg")]
    public void Create_TradeMissingOrMalformed_Fails_CA_047_08(string? trade) =>
        new CreateServiceBodyValidator().Validate(Valid() with { Oficio = trade }).IsValid.Should().BeFalse();

    [Fact]
    public void Update_EmptyBody_IsValidBecauseOnlySentFieldsChange_CA_047_12() =>
        new UpdateServiceBodyValidator().Validate(new UpdateServiceBody(null, null, null, null)).IsValid.Should().BeTrue();

    [Fact]
    public void Update_SentFieldsFollowTheSameRules_CA_047_14()
    {
        new UpdateServiceBodyValidator().Validate(new UpdateServiceBody(null, null, 0, null)).IsValid.Should().BeFalse();
        new UpdateServiceBodyValidator().Validate(new UpdateServiceBody("", null, null, null)).IsValid.Should().BeFalse();
        new UpdateServiceBodyValidator().Validate(new UpdateServiceBody("Nuevo nombre", "detalle", 100, "mec")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Routes_RequireAnObjectId_CA_047_02_CA_047_19()
    {
        var valid = ObjectId.GenerateNewId().ToString();

        new ServiceRouteValidator().Validate(new ServiceRoute { Id = valid }).IsValid.Should().BeTrue();
        new ServiceRouteValidator().Validate(new ServiceRoute { Id = "no-es-un-id" }).IsValid.Should().BeFalse();
        new TechnicianServicesRouteValidator().Validate(new TechnicianServicesRoute { Id = valid }).IsValid.Should().BeTrue();
        new TechnicianServicesRouteValidator().Validate(new TechnicianServicesRoute { Id = null }).IsValid.Should().BeFalse();
    }
}
