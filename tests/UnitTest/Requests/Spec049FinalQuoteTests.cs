using AwesomeAssertions;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;
using Knock.Market.Api.Modules.Requests.Validators;
using Xunit;

namespace Knock.UnitTest.Requests;

/// <summary>Spec 049, R-13 a R-16: plazos, monto mínimo del ajuste y formato de los cuerpos. Funciones puras.</summary>
public sealed class Spec049FinalQuoteTests
{
    private static readonly DateTime _now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FinalQuoteExpiresAt_Is48HoursAfterSending() =>
        FinalQuoteRules.FinalQuoteExpiresAt(_now).Should().Be(_now.AddHours(48));

    [Fact]
    public void AdjustmentExpiresAt_Is24HoursAfterProposing() =>
        FinalQuoteRules.AdjustmentExpiresAt(_now).Should().Be(_now.AddHours(24));

    [Theory]
    [InlineData(1000, 1100)]
    [InlineData(int.MaxValue, (long)int.MaxValue + 100)]
    public void MinimumAdjustedAmount_IsCapPlusOneHundred_AndNeverOverflows(int cap, long expected) =>
        FinalQuoteRules.MinimumAdjustedAmount(cap).Should().Be(expected);

    [Theory]
    [InlineData("aprueba", true)]
    [InlineData("rechaza", true)]
    [InlineData("APRUEBA", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsDecision_AcceptsOnlyTheTwoValues(string? value, bool expected) =>
        FinalQuoteRules.IsDecision(value).Should().Be(expected);

    [Theory]
    [InlineData(1, "Cambio de tubería", true)]
    [InlineData(2_000_000, "Cambio de tubería", true)]
    [InlineData(0, "Cambio de tubería", false)]
    [InlineData(2_000_001, "Cambio de tubería", false)]
    [InlineData(500, "corto", false)]
    [InlineData(null, "Cambio de tubería", false)]
    public void FinalQuoteBodyValidator_ChecksPriceAndDescription(int? price, string description, bool valid) =>
        new FinalQuoteBodyValidator().Validate(new FinalQuoteBody(price, description)).IsValid.Should().Be(valid);

    [Fact]
    public void FinalQuoteBodyValidator_RejectsDescriptionOver500() =>
        new FinalQuoteBodyValidator().Validate(new FinalQuoteBody(500, new string('a', 501))).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("aprueba", true)]
    [InlineData("rechaza", true)]
    [InlineData("quizá", false)]
    [InlineData(null, false)]
    public void DecisionBodyValidator_ChecksTheValue(string? decision, bool valid) =>
        new DecisionBodyValidator().Validate(new DecisionBody(decision)).IsValid.Should().Be(valid);

    [Theory]
    [InlineData(1500, "Material más caro de lo previsto", true)]
    [InlineData(1500, "corto", false)]
    [InlineData(0, "Material más caro de lo previsto", false)]
    [InlineData(null, "Material más caro de lo previsto", false)]
    public void AdjustmentBodyValidator_ChecksAmountAndReason(int? amount, string reason, bool valid) =>
        new AdjustmentBodyValidator().Validate(new AdjustmentBody(amount, reason)).IsValid.Should().Be(valid);
}
