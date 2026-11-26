using AwesomeAssertions;
using Knock.Market.Api.Modules.Requests.Domain;
using Xunit;

namespace Knock.UnitTest.Requests;

public class Spec048MarketReferenceMathTests
{
    [Fact]
    public void FewerThanTenPrices_GiveNoReference_CA_048_19()
    {
        MarketReferenceMath.Summarize([100, 200, 300, 400, 500, 600, 700, 800, 900], "mercado").Should().BeNull();
    }

    [Fact]
    public void TenOrMorePrices_GiveQuartilesAndMedian_CA_048_19()
    {
        var reference = MarketReferenceMath.Summarize([1200, 100, 900, 200, 1100, 300, 1000, 400, 500, 600, 700, 800], "mercado");
        reference.Should().NotBeNull();
        reference.Origen.Should().Be("mercado");
        reference.P25.Should().Be(375);
        reference.Mediana.Should().Be(650);
        reference.P75.Should().Be(925);
    }

    [Fact]
    public void EqualPrices_GiveTheSameValueEverywhere_CA_048_19()
    {
        var reference = MarketReferenceMath.Summarize([250, 250, 250, 250, 250, 250, 250, 250, 250, 250], "categoria");
        reference.Should().Be(new Knock.Market.Api.Modules.Requests.Dtos.MarketReference("categoria", 250, 250, 250));
    }
}
