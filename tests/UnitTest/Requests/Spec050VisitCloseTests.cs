using AwesomeAssertions;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;
using Knock.Market.Api.Modules.Requests.Validators;
using Xunit;

namespace Knock.UnitTest.Requests;

/// <summary>Spec 050, R-28 a R-30: textos, fotos y plazos del cierre de la visita y formato de los cuerpos. Funciones puras.</summary>
public sealed class Spec050VisitCloseTests
{
    private static readonly DateTime _now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string[] _photo = ["64b7f0c2a1b2c3d4e5f60718"];

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(7 * 24 * 60, true)]
    [InlineData(7 * 24 * 60 + 1, false)]
    public void IsInReportWindow_IsFinishedUpTo7DaysAfter_CA_050_40(int minutesSinceFinished, bool expected) =>
        VisitCloseRules.IsInReportWindow(_now, _now.AddMinutes(-minutesSinceFinished)).Should().Be(expected);

    [Theory]
    [InlineData("1234567", false)]
    [InlineData("12345678", true)]
    [InlineData(null, false)]
    public void IsValidDescription_Needs8To500Characters_CA_050_32(string? text, bool expected) =>
        VisitCloseRules.IsValidDescription(text).Should().Be(expected);

    [Fact]
    public void IsValidRecommendation_IsOptionalAndUpTo300_CA_050_32()
    {
        VisitCloseRules.IsValidRecommendation(null).Should().BeTrue();
        VisitCloseRules.IsValidRecommendation(new string('a', 300)).Should().BeTrue();
        VisitCloseRules.IsValidRecommendation(new string('a', 301)).Should().BeFalse();
    }

    [Fact]
    public void FinishValidator_RequiresBetween1And5DistinctPhotosAndNeverRepeatsTheValue_CA_050_49()
    {
        var validator = new FinishVisitBodyValidator();

        validator.Validate(new FinishVisitBody("Cambio de llave", null, _photo)).IsValid.Should().BeTrue();
        validator.Validate(new FinishVisitBody("Cambio de llave", null, [])).IsValid.Should().BeFalse();
        validator.Validate(new FinishVisitBody("Cambio de llave", null, null)).IsValid.Should().BeFalse();
        validator.Validate(new FinishVisitBody("Cambio de llave", null, ["no-es-id"])).IsValid.Should().BeFalse();
        validator.Validate(new FinishVisitBody("Cambio de llave", null, [_photo[0], _photo[0].ToUpperInvariant()])).IsValid.Should().BeFalse();
        validator.Validate(new FinishVisitBody("Cambio de llave", null, [.. Enumerable.Range(0, 6).Select(i => $"64b7f0c2a1b2c3d4e5f6071{i}")])).IsValid.Should().BeFalse();
        var short_ = validator.Validate(new FinishVisitBody("corto", null, _photo));
        short_.Errors.Should().OnlyContain(e => e.ErrorMessage == FinishVisitBodyValidator.FormatReason);
    }

    [Fact]
    public void NotFinishedValidator_Needs8To500Characters_CA_050_38()
    {
        var validator = new NotFinishedBodyValidator();

        validator.Validate(new NotFinishedBody("no quedo arreglado")).IsValid.Should().BeTrue();
        validator.Validate(new NotFinishedBody("corto")).IsValid.Should().BeFalse();
        validator.Validate(new NotFinishedBody(null)).IsValid.Should().BeFalse();
        validator.Validate(new NotFinishedBody(new string('a', 501))).IsValid.Should().BeFalse();
    }
}
