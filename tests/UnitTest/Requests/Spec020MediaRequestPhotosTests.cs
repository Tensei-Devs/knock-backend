using AwesomeAssertions;
using Knock.Market.Api.Modules.Media;
using Knock.Market.Api.Modules.Requests.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Knock.UnitTest.Requests;

/// <summary>Spec 020 (R-04, CA-18) y 032: el adaptador de fotos de la solicitud comparte la comprobación del de visitas y no inventa enlaces de lectura.</summary>
public sealed class Spec020MediaRequestPhotosTests
{
    private const string _owner = "client-sub";
    private const string _photo = "018f3c2e7a4b7c3d9e1f2a3b4c5d6e7f";

    [Fact]
    public async Task AreReadyForAsync_AsksMediaWithTheClientSub_CA_020_18()
    {
        var media = new FakeMediaApi((_, _) => true);

        (await Adapter(media).AreReadyForAsync(_owner, [_photo], CancellationToken.None)).Should().BeTrue();

        media.Calls.Should().Equal((Guid.ParseExact(_photo, "N"), _owner));
    }

    [Fact]
    public async Task AreReadyForAsync_ObjectIdFromTheOldFormat_FailsClosedWithoutAskingMedia_CA_020_18()
    {
        var media = new FakeMediaApi((_, _) => true);

        (await Adapter(media).AreReadyForAsync(_owner, ["64b7f0c2a1b2c3d4e5f60718"], CancellationToken.None)).Should().BeFalse();

        media.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadLinksAsync_WithoutReadLinksInTheMediaModule_ReturnsNoPhotos_R_04()
    {
        (await Adapter(new FakeMediaApi((_, _) => true)).ReadLinksAsync([_photo], CancellationToken.None)).Should().BeEmpty();
    }

    private static MediaRequestPhotos Adapter(IMediaApi media) => new(media, NullLogger<MediaRequestPhotos>.Instance);
}
