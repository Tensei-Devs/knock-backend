using AwesomeAssertions;
using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Modules.Catalog.Data;
using Knock.Market.Api.Modules.Catalog.Domain;
using Knock.Market.Api.Modules.Catalog.Dtos;
using Knock.Market.Api.Modules.Catalog.Validators;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Knock.UnitTest.Catalog;

/// <summary>Spec 045, T-09: reglas de negocio (R-18 a R-22), criterios CA-33 a CA-37. CA-34 y CA-36 con base de datos están en <c>Spec045OficioCacheTests</c>; CA-35 en las pruebas de alta y edición; CA-37 en <c>Spec045AdminTests</c>.</summary>
public sealed partial class Spec045OficiosTests
{
    [Fact]
    public async Task ARepeatedKey_Answers409_APercentageOutOfRangeANameTooLongOrAKeyWithSpaces_Answer422_CA_045_33()
    {
        var store = new FakeTradeAdminStore();
        store.Trades.Add(Trade("gas", "Gas"));
        var validator = new CreateTradeBodyValidator();

        var taken = async () => await Admin(AdminService(store)).Create(NewBody("gas"), CancellationToken.None);
        (await taken.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be(TradeErrorCodes.TradeKeyTakenCode);
        (await validator.ValidateAsync(NewBody() with { TopePorcentaje = 120m })).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(NewBody() with { Nombre = new string('x', 41) })).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(NewBody("con espacio"))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task TheEdit_NeverChangesTheKey_CA_045_33()
    {
        var fixture = new AdminTradesControllerFixture();

        var result = await PutAsync(fixture, "plomeria", Edit() with { Clave = "otra" });

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(422);
        fixture.Store.Trades.Should().ContainSingle().Which.Clave.Should().Be("plomeria");
        typeof(TradeChanges).GetProperties().Select(p => p.Name).Should().NotContain("Clave", "el almacén no tiene cómo cambiar la clave");
    }

    [Fact]
    public void TheTradeParameters_HaveTheRangesOfTheSpec_CA_045_33()
    {
        TradeRules.IsValidPercentage(0m).Should().BeTrue();
        TradeRules.IsValidPercentage(100m).Should().BeTrue();
        TradeRules.IsValidPercentage(100.01m).Should().BeFalse();
        TradeRules.IsValidPercentage(-0.01m).Should().BeFalse();
        TradeRules.IsValidPercentage(null).Should().BeFalse();
        TradeRules.IsPositive(1).Should().BeTrue();
        TradeRules.IsPositive(0).Should().BeFalse();
        TradeRules.IsValidModality("ambos").Should().BeTrue();
        TradeRules.IsValidModality("Domicilio").Should().BeFalse("la modalidad distingue mayúsculas, como la del técnico");
    }

    [Theory]
    [InlineData("Plomero", "plomero ")]
    [InlineData("PLOMERO", "  plomero")]
    [InlineData("Plómero", "plomero")]
    [InlineData("Técnico   en   A/C", "tecnico en a c")]
    public void TwoTexts_ThatDifferOnlyByAccentsCaseOrSpaces_HaveTheSameNormalizedForm_CA_045_36(string one, string other) =>
        TradeRules.NormalizeRequestText(one).Should().Be(TradeRules.NormalizeRequestText(other));

    [Fact]
    public async Task TwoTechniciansAskingForTheSameTrade_BothSaveAndTheSamePersonRepeatsNothing_CA_045_36()
    {
        var store = new FakeRequestStore(_technician, "tecnico-2");

        var one = await Requests(store).Create(new TradeRequestBody("Plomero"), CancellationToken.None);
        var again = await Requests(store).Create(new TradeRequestBody("Plomero "), CancellationToken.None);
        await Requests(store, sub: "tecnico-2").Create(new TradeRequestBody("plomero"), CancellationToken.None);

        Created(again).Id.Should().Be(Created(one).Id);
        store.Saved.Should().HaveCount(2);
        store.Saved.Select(s => s.Normalized).Distinct().Should().ContainSingle().Which.Should().Be("plomero");
    }

    [Fact]
    public void TheModalities_AreTheOnesOfTheTechnicianAndTheVisit_CA_045_33()
    {
        TradeRules.Modalities.Should().Equal("domicilio", "taller", "ambos");
        TradeRules.MaxNameLength.Should().Be(40);
        TradeRules.MaxRequestTextLength.Should().Be(60);
    }
}
