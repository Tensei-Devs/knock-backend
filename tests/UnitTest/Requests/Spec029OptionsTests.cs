using AwesomeAssertions;
using Knock.Market.Api.Modules.Requests.Domain;
using Xunit;

namespace Knock.UnitTest.Requests;

/// <summary>Spec 029, R-08 y CA-14: el plazo de vencimiento es de 1 a 168 horas (48 por omisión) y el barrido de 1 a 1440 minutos.</summary>
public sealed class Spec029OptionsTests
{
    [Fact]
    public void Defaults_Are_48_Hours_And_10_Minutes_CA_029_14()
    {
        var options = new SolicitudesOptions();

        options.HorasVencimiento.Should().Be(48);
        options.MinutosBarrido.Should().Be(10);
        options.IsValid().Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 10, false)]
    [InlineData(1, 10, true)]
    [InlineData(24, 10, true)]
    [InlineData(168, 10, true)]
    [InlineData(169, 10, false)]
    [InlineData(200, 10, false)]
    [InlineData(48, 0, false)]
    [InlineData(48, 1441, false)]
    public void Out_Of_Range_Values_Are_Invalid_CA_029_14(int hours, int minutes, bool valid) =>
        new SolicitudesOptions { HorasVencimiento = hours, MinutosBarrido = minutes }.IsValid().Should().Be(valid);

    [Fact]
    public void Lifetime_Uses_The_Configured_Hours_CA_029_14() =>
        RequestRules.ExpiresAt(new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero), 24).Should().Be(new DateTimeOffset(2026, 11, 21, 0, 0, 0, TimeSpan.Zero));
}
