using AwesomeAssertions;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;
using Knock.Market.Api.Modules.Requests.Validators;
using Xunit;

namespace Knock.UnitTest.Requests;

/// <summary>Spec 050, R-24 a R-27: rango de la agenda, plazos de reprogramar, cancelación tardía, ventana de ausencia y formato de los cuerpos. Funciones puras.</summary>
public sealed class Spec050VisitScheduleTests
{
    private static readonly DateTime _now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AppointmentUtc_ConvertsMexicoCityTimeToUtc_CA_050_45() =>
        VisitScheduleRules.AppointmentUtc("2026-11-23", "10:30").Should().Be(new DateTime(2026, 11, 23, 16, 30, 0, DateTimeKind.Utc));

    [Fact]
    public void ToLocal_IsTheInverseOfAppointmentUtc_CA_050_46()
    {
        var appointment = VisitScheduleRules.AppointmentUtc("2026-11-23", "10:30");

        VisitScheduleRules.ToLocal(appointment).Should().Be(("2026-11-23", "10:30"));
    }

    [Theory]
    [InlineData("2026-11-01", "2026-11-14", true)]
    [InlineData("2026-11-01", "2026-12-01", true)]
    [InlineData("2026-11-01", "2026-12-02", false)]
    [InlineData("2026-11-05", "2026-11-04", false)]
    public void IsValidAgendaRange_AllowsUpTo31DaysAndNeverInverted_CA_050_45(string from, string to, bool expected) =>
        VisitScheduleRules.IsValidAgendaRange(DateOnly.Parse(from, System.Globalization.CultureInfo.InvariantCulture), DateOnly.Parse(to, System.Globalization.CultureInfo.InvariantCulture)).Should().Be(expected);

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(24 * 30, true)]
    [InlineData(24 * 30 + 1, false)]
    public void IsValidProposal_NeedsAtLeast2HoursAndAtMost30Days_CA_050_09(int hoursAhead, bool expected) =>
        VisitScheduleRules.IsValidProposal(_now.AddHours(hoursAhead), _now).Should().Be(expected);

    [Fact]
    public void ProposalExpiresAt_Is24HoursWhenTheVisitIsFar_CA_050_46() =>
        VisitScheduleRules.ProposalExpiresAt(_now, _now.AddDays(5)).Should().Be(_now.AddHours(24));

    [Fact]
    public void ProposalExpiresAt_Is2HoursBeforeTheVisitWhenThatIsSooner_CA_050_46() =>
        VisitScheduleRules.ProposalExpiresAt(_now, _now.AddHours(10)).Should().Be(_now.AddHours(8));

    [Fact]
    public void ProposalExpiresAt_IsNullWhenThereIsNoTimeToAnswer_CA_050_11() =>
        VisitScheduleRules.ProposalExpiresAt(_now, _now.AddHours(2)).Should().BeNull();

    [Theory]
    [InlineData(25, false)]
    [InlineData(24, false)]
    [InlineData(23, true)]
    public void IsLateCancellation_IsTrueUnder24Hours_CA_050_47(int hoursAhead, bool expected) =>
        VisitScheduleRules.IsLateCancellation(_now, _now.AddHours(hoursAhead)).Should().Be(expected);

    [Theory]
    [InlineData(-14, false)]
    [InlineData(-15, true)]
    [InlineData(-120, true)]
    [InlineData(-121, false)]
    public void IsInAbsenceWindow_IsFrom15MinutesTo2HoursAfterTheAppointment_CA_050_48(int minutesFromAppointment, bool expected) =>
        VisitScheduleRules.IsInAbsenceWindow(_now, _now.AddMinutes(minutesFromAppointment)).Should().Be(expected);

    [Theory]
    [InlineData("1234567", false)]
    [InlineData("12345678", true)]
    [InlineData("   1234567   ", false)]
    [InlineData(null, false)]
    public void IsValidReason_Needs8To300Characters_CA_050_21(string? reason, bool expected) =>
        VisitScheduleRules.IsValidReason(reason).Should().Be(expected);

    [Fact]
    public void AgendaQueryValidator_RejectsABadDayAnInvertedRangeAndALimitOver50_CA_050_04()
    {
        var validator = new AgendaQueryValidator();

        validator.Validate(new AgendaQuery { Desde = "2026-13-40" }).Errors.Should().ContainSingle(e => e.PropertyName == "Desde");
        validator.Validate(new AgendaQuery { Desde = "2026-11-05", Hasta = "2026-11-04" }).IsValid.Should().BeFalse();
        validator.Validate(new AgendaQuery { Desde = "2026-11-01", Hasta = "2026-12-05" }).IsValid.Should().BeFalse();
        validator.Validate(new AgendaQuery { Limite = 51 }).Errors.Should().ContainSingle(e => e.PropertyName == "Limite");
        validator.Validate(new AgendaQuery { Desde = "2026-11-01", Hasta = "2026-11-20", Limite = 50 }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void BodyValidators_RequireTheirFieldsAndNeverRepeatTheValue_CA_050_09()
    {
        new RescheduleBodyValidator().Validate(new RescheduleBody(null)).IsValid.Should().BeFalse();
        new RescheduleBodyValidator().Validate(new RescheduleBody(_now)).IsValid.Should().BeTrue();
        new RescheduleReplyBodyValidator().Validate(new RescheduleReplyBody(null)).IsValid.Should().BeFalse();
        new RescheduleReplyBodyValidator().Validate(new RescheduleReplyBody(false)).IsValid.Should().BeTrue();
        var result = new CancelVisitBodyValidator().Validate(new CancelVisitBody("corto"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().OnlyContain(e => e.ErrorMessage == CancelVisitBodyValidator.FormatReason);
        new CancelVisitBodyValidator().Validate(new CancelVisitBody("Ya no necesito la visita")).IsValid.Should().BeTrue();
    }
}
