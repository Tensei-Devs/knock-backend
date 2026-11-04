using AwesomeAssertions;
using Knock.Contracts.Correlation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Knock.UnitTest.Correlation;

public class CorrelationIdTests
{
    [Theory]
    [InlineData("abc", true)]
    [InlineData("abc-123_X.y", true)]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123", true)]
    [InlineData("01234567890123456789012345678901234567890123456789012345678901234", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("con espacio", false)]
    [InlineData("a/b", false)]
    [InlineData("a\r\nb", false)]
    public void IsValid_FollowsTheSharedRule_R_05(string? value, bool expected)
    {
        CorrelationId.IsValid(value).Should().Be(expected);
    }

    [Fact]
    public void Create_ReturnsAValidIdWithoutDashes_R_05()
    {
        var id = CorrelationId.Create();

        id.Should().HaveLength(32);
        id.Should().NotContain("-");
        CorrelationId.IsValid(id).Should().BeTrue();
    }

    [Fact]
    public async Task Middleware_WithoutHeader_GeneratesAnIdAndPassesItOn_CA_02()
    {
        string? seenByNext = null;
        string? headerSeenByNext = null;
        var middleware = new CorrelationIdMiddleware(
            context =>
            {
                seenByNext = CorrelationId.Current(context);
                headerSeenByNext = context.Request.Headers[CorrelationId.HeaderName];
                return Task.CompletedTask;
            },
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(new DefaultHttpContext());

        seenByNext.Should().NotBeNullOrEmpty();
        CorrelationId.IsValid(seenByNext).Should().BeTrue();
        headerSeenByNext.Should().Be(seenByNext);
    }

    [Fact]
    public async Task Middleware_WithAValidHeader_ReusesIt_CA_02()
    {
        string? seenByNext = null;
        var middleware = new CorrelationIdMiddleware(
            context =>
            {
                seenByNext = CorrelationId.Current(context);
                return Task.CompletedTask;
            },
            NullLogger<CorrelationIdMiddleware>.Instance);
        var http = new DefaultHttpContext();
        http.Request.Headers[CorrelationId.HeaderName] = "cliente-123";

        await middleware.InvokeAsync(http);

        seenByNext.Should().Be("cliente-123");
    }

    [Fact]
    public async Task Middleware_WithAnInvalidHeader_ReplacesIt_CA_02()
    {
        string? seenByNext = null;
        var middleware = new CorrelationIdMiddleware(
            context =>
            {
                seenByNext = CorrelationId.Current(context);
                return Task.CompletedTask;
            },
            NullLogger<CorrelationIdMiddleware>.Instance);
        var http = new DefaultHttpContext();
        http.Request.Headers[CorrelationId.HeaderName] = "no valido!";

        await middleware.InvokeAsync(http);

        seenByNext.Should().NotBe("no valido!");
        CorrelationId.IsValid(seenByNext).Should().BeTrue();
    }

    [Fact]
    public void Current_WithoutTheMiddleware_ReturnsNull_R_05()
    {
        CorrelationId.Current(new DefaultHttpContext()).Should().BeNull();
    }
}
