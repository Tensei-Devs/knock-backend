using AwesomeAssertions;
using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Modules.Requests.Domain;
using Xunit;

namespace Knock.UnitTest.Requests;

public class Spec020RequestStateTests
{
    [Fact]
    public void ExpiryIs48HoursAfterCreation_R_01()
    {
        var created = new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);

        RequestRules.ExpiresAt(created).Should().Be(created.AddHours(48));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(48)]
    [InlineData(168)]
    public void ExpiryUsesClockAndConfiguredHours_R_01(int hours)
    {
        var now = new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);
        RequestRules.ExpiresAt(new FixedClock(now), hours).Should().Be(now.AddHours(hours));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(169)]
    public void ExpiryRejectsHoursOutsideConfiguredRange_R_01(int hours)
    {
        var act = () => RequestRules.ExpiresAt(TimeProvider.System, hours);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ExpiryDefaultsTo48HoursWithClock_R_01()
    {
        var now = new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);
        RequestRules.ExpiresAt(new FixedClock(now)).Should().Be(now.AddHours(48));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Theory]
    [InlineData("Enviada", true)]
    [InlineData("Cotizando", true)]
    [InlineData("CitaConfirmada", false)]
    [InlineData("Terminada", false)]
    [InlineData("Calificada", false)]
    [InlineData("Cancelada", false)]
    [InlineData("Vencida", false)]
    public void OnlySentOrQuotingRequestsAcceptQuotes_R_16(string stateName, bool accepts)
    {
        RequestRules.AcceptsQuotes(Enum.Parse<RequestState>(stateName)).Should().Be(accepts);
    }

    [Fact]
    public void FirstQuoteMovesASentRequestToQuotingAndNothingElse_R_16()
    {
        RequestRules.AfterFirstQuote(RequestState.Enviada).Should().Be(RequestState.Cotizando);
        RequestRules.AfterFirstQuote(RequestState.Cotizando).Should().Be(RequestState.Cotizando);
    }

    [Theory]
    [InlineData("CitaConfirmada")]
    [InlineData("Terminada")]
    [InlineData("Calificada")]
    [InlineData("Cancelada")]
    [InlineData("Vencida")]
    public void FirstQuoteOnAClosedRequestIsRejected_R_16(string stateName)
    {
        var act = () => RequestRules.AfterFirstQuote(Enum.Parse<RequestState>(stateName));

        act.Should().Throw<DomainException>().Which.Kind.Should().Be(DomainErrorKind.Conflict);
    }

    [Theory]
    [InlineData("Enviada", true)]
    [InlineData("Aceptada", true)]
    [InlineData("Rechazada", false)]
    [InlineData("Retirada", false)]
    public void OnlySentOrAcceptedQuotesAreLive_R_20(string stateName, bool live)
    {
        RequestRules.IsLive(Enum.Parse<QuoteState>(stateName)).Should().Be(live);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(6, false)]
    public void TheSixthLiveQuoteIsRefused_R_20(int liveQuotes, bool canAdd)
    {
        RequestRules.CanAddQuote(liveQuotes).Should().Be(canAdd);
    }

    [Theory]
    [InlineData("Enviada", "enviada")]
    [InlineData("CitaConfirmada", "cita_confirmada")]
    [InlineData("Vencida", "vencida")]
    public void RequestStatesUseTheStoredNames(string stateName, string stored)
    {
        var state = Enum.Parse<RequestState>(stateName);
        RequestStates.ToStored(state).Should().Be(stored);
        RequestStates.ParseRequest(stored).Should().Be(state);
    }

    [Theory]
    [InlineData("Aceptada", "aceptada")]
    [InlineData("Retirada", "retirada")]
    public void QuoteStatesUseTheStoredNames(string stateName, string stored)
    {
        var state = Enum.Parse<QuoteState>(stateName);
        RequestStates.ToStored(state).Should().Be(stored);
        RequestStates.ParseQuote(stored).Should().Be(state);
    }
}
