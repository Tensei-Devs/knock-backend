using AwesomeAssertions;
using Knock.Market.Api.Modules.Media.Domain;
using Xunit;

namespace Knock.UnitTest.Media;

public class MediaRulesTests
{
    [Theory]
    [InlineData("image/jpeg", true)]
    [InlineData("image/png", true)]
    [InlineData("image/gif", false)]
    public void IsAllowedContentType_AcceptsOnlyJpegAndPng_OI1_01(string contentType, bool expected)
    {
        MediaRules.IsAllowedContentType(contentType).Should().Be(expected);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(8388608, true)]
    [InlineData(0, false)]
    [InlineData(8388609, false)]
    public void IsAllowedSize_AcceptsOneTo8Mb_OI1_01(long sizeBytes, bool expected)
    {
        MediaRules.IsAllowedSize(sizeBytes).Should().Be(expected);
    }

    [Theory]
    [InlineData("/9j/AA==", 0)]
    [InlineData("iVBORw0KGgo=", 1)]
    [InlineData("AAAAAAAAAAA=", null)]
    public void DetectFormat_DetectsBySignature_OI1_01(string base64, int? expected)
    {
        var format = MediaRules.DetectFormat(Convert.FromBase64String(base64));

        format.Should().Be(expected.HasValue ? (ImageFormat)expected.Value : (ImageFormat?)null);
    }

    [Theory]
    [InlineData("perfil", 0)]
    [InlineData("solicitud", 1)]
    [InlineData("otro", null)]
    public void ParseUsage_AcceptsContractValues_OI1_01(string value, int? expected)
    {
        MediaRules.ParseUsage(value).Should().Be(expected.HasValue ? (MediaUsage)expected.Value : (MediaUsage?)null);
    }

    [Fact]
    public void SasLifetime_IsFiveMinutes_OI1_02()
    {
        MediaRules.SasLifetime.Should().Be(TimeSpan.FromMinutes(5));
    }
}
