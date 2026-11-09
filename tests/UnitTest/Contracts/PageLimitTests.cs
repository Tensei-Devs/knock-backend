using AwesomeAssertions;
using Knock.Contracts.Pagination;
using Xunit;

namespace Knock.UnitTest.Contracts;

/// <summary>Spec 002, R-05: la paginación por cursor común es de 20 por página y de 50 como máximo.</summary>
public class PageLimitTests
{
    [Fact]
    public void Limits_AreTwentyByDefaultAndFiftyAtMost_R_05()
    {
        PageLimit.Default.Should().Be(20);
        PageLimit.Max.Should().Be(50);
    }

    [Theory]
    [InlineData(null, 20)]
    [InlineData(0, 20)]
    [InlineData(-5, 20)]
    [InlineData(1, 1)]
    [InlineData(37, 37)]
    [InlineData(50, 50)]
    [InlineData(51, 50)]
    [InlineData(int.MaxValue, 50)]
    public void Resolve_NeverReturnsMoreThanTheMaximum_R_05(int? requested, int expected)
    {
        PageLimit.Resolve(requested).Should().Be(expected);
    }

    [Fact]
    public void LastPage_HasNoNextCursor_R_05()
    {
        var page = new CursorPage<string>(["a", "b"], null);

        page.Items.Should().HaveCount(2);
        page.NextCursor.Should().BeNull();
    }
}
