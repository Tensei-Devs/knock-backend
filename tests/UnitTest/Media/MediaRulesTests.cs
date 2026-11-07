using AwesomeAssertions;
using Knock.Market.Api.Modules.Media.Domain;
using Xunit;

namespace Knock.UnitTest.Media;

public class MediaRulesTests
{
    [Fact] public void IsAllowedContentType_AcceptsJpegAndPngOnly_OI1_01() { MediaRules.IsAllowedContentType("image/jpeg").Should().BeTrue(); MediaRules.IsAllowedContentType("image/png").Should().BeTrue(); MediaRules.IsAllowedContentType("image/gif").Should().BeFalse(); }
    [Fact] public void IsAllowedSize_AcceptsOneTo8Mb_OI1_01() { MediaRules.IsAllowedSize(1).Should().BeTrue(); MediaRules.IsAllowedSize(MediaRules.MaxFileBytes).Should().BeTrue(); MediaRules.IsAllowedSize(0).Should().BeFalse(); MediaRules.IsAllowedSize(MediaRules.MaxFileBytes + 1).Should().BeFalse(); }
    [Fact] public void DetectFormat_DetectsBySignature_OI1_01() { MediaRules.DetectFormat([0xFF, 0xD8, 0xFF, 0x00]).Should().Be(ImageFormat.Jpeg); MediaRules.DetectFormat([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]).Should().Be(ImageFormat.Png); MediaRules.DetectFormat(new byte[8]).Should().BeNull(); }
    [Fact] public void ParseUsage_AcceptsContractValues_OI1_01() { MediaRules.ParseUsage("perfil").Should().Be(MediaUsage.Profile); MediaRules.ParseUsage("solicitud").Should().Be(MediaUsage.Request); MediaRules.ParseUsage("otro").Should().BeNull(); }
    [Fact] public void SasLifetime_IsFiveMinutes_OI1_02() { MediaRules.SasLifetime.Should().Be(TimeSpan.FromMinutes(5)); }
}
