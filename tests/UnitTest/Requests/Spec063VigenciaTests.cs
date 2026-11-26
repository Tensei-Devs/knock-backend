using AwesomeAssertions;
using Knock.Market.Api.Modules.Requests.Domain;
using Xunit;

namespace Knock.UnitTest.Requests;

/// <summary>Spec 063, R-02 y R-05: el estado que se informa al leer se calcula con la hora, sin escribir. Funciones puras.</summary>
public sealed class Spec063VigenciaTests
{
    private static readonly DateTime _now = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(-1, "vencida")]
    [InlineData(0, "vencida")]
    [InlineData(1, "enviada")]
    public void EffectiveState_SentQuote_IsExpiredOnceItsDeadlinePassed_CA_063_04(int secondsFromNow, string expected) =>
        FinalQuoteRules.EffectiveState(FinalQuoteRules.Sent, _now.AddSeconds(secondsFromNow), _now).Should().Be(expected);

    [Theory]
    [InlineData("aprobada")]
    [InlineData("rechazada")]
    [InlineData("vencida")]
    public void EffectiveState_DecidedQuote_KeepsItsStoredState_CA_063_05(string stored) =>
        FinalQuoteRules.EffectiveState(stored, _now.AddDays(-2), _now).Should().Be(stored);

    [Theory]
    [InlineData("pendiente", 1, true)]
    [InlineData("pendiente", 0, false)]
    [InlineData("pendiente", -1, false)]
    [InlineData("aprobado", 3600, false)]
    [InlineData("rechazado", 3600, false)]
    [InlineData("vencido", 3600, false)]
    public void IsAdjustmentLive_OnlyPendingAndNotExpired_CA_063_07(string stored, int secondsFromNow, bool expected) =>
        FinalQuoteRules.IsAdjustmentLive(stored, _now.AddSeconds(secondsFromNow), _now).Should().Be(expected);
}
