using AwesomeAssertions;
using Knock.Market.Api.Modules.Requests.Domain;
using Xunit;

namespace Knock.UnitTest.Requests;

/// <summary>Spec 064, R-02 y R-03: la ventana del reporte «el técnico no llegó» y la idempotencia por estado. Funciones puras.</summary>
public sealed class Spec064VentanaTests
{
    private static readonly DateTime _appointment = new(2026, 11, 23, 16, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, false)]
    [InlineData(14, false)]
    [InlineData(15, true)]
    [InlineData(20, true)]
    [InlineData(120, true)]
    [InlineData(121, false)]
    public void CanReportTechnicianAbsent_ConfirmedVisit_OnlyFrom15MinutesTo2HoursAfter_CA_064_03(int minutesAfterAppointment, bool expected) =>
        VisitScheduleRules.CanReportTechnicianAbsent("confirmada", _appointment.AddMinutes(minutesAfterAppointment), _appointment).Should().Be(expected);

    [Theory]
    [InlineData("en_curso")]
    [InlineData("terminada")]
    [InlineData("cancelada")]
    [InlineData("no_presentado")]
    public void CanReportTechnicianAbsent_VisitNotConfirmed_IsNeverAllowed_CA_064_03(string state) =>
        VisitScheduleRules.CanReportTechnicianAbsent(state, _appointment.AddMinutes(30), _appointment).Should().BeFalse();

    [Theory]
    [InlineData("no_presentado", "tecnico", true)]
    [InlineData("no_presentado", "cliente", false)]
    [InlineData("no_presentado", null, false)]
    [InlineData("confirmada", "tecnico", false)]
    public void IsTechnicianAbsentRecorded_OnlyWhenTheTechnicianWasMarked_CA_064_05(string state, string? whoMissed, bool expected) =>
        VisitScheduleRules.IsTechnicianAbsentRecorded(state, whoMissed).Should().Be(expected);
}
