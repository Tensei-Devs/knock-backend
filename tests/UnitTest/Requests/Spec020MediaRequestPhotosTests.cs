using AwesomeAssertions;
using Knock.Market.Api.Modules.Media;
using Knock.Market.Api.Modules.Requests.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Knock.UnitTest.Requests;

/// <summary>Spec 020 (R-04, CA-18) y 032: el adaptador de fotos de la solicitud comparte la comprobación del de visitas (con el uso solicitud) y emite los enlaces de lectura del módulo de archivos.</summary>
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
    public async Task AreReadyForAsync_AsksForTheRequestUse_CA_020_18()
    {
        var media = new FakeMediaApi((_, _) => true);

        await Adapter(media).AreReadyForAsync(_owner, [_photo], CancellationToken.None);

        media.Uses.Should().Equal("solicitud");
    }

    [Fact]
    public async Task ReadLinksAsync_AsksMediaWithTheRequesterAndMapsTheLinks_R_04()
    {
        var media = new FakeMediaApi((_, _) => true);

        var photos = await Adapter(media).ReadLinksAsync("technician-sub", [_photo], CancellationToken.None);

        var request = media.LinkRequests.Should().ContainSingle().Subject;
        request.Requester.Should().Be("technician-sub");
        request.Use.Should().Be("solicitud");
        request.Files.Should().Equal(Guid.ParseExact(_photo, "N"));
        var item = photos.Should().ContainSingle().Subject;
        item.Id.Should().Be(_photo);
        item.Url.Should().StartWith("https://files.test/");
        item.VenceEn.Should().Be(new DateTimeOffset(2026, 11, 22, 12, 5, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task ReadLinksAsync_SkipsIdentifiersThatAreNotFileIdsAndKeepsOrder_R_04()
    {
        var media = new FakeMediaApi((_, _) => true);
        const string second = "018f3c2e7a4b7c3d9e1f2a3b4c5d6e80";

        var photos = await Adapter(media).ReadLinksAsync("client-sub", [second, "64b7f0c2a1b2c3d4e5f60718", _photo], CancellationToken.None);

        photos.Select(p => p.Id).Should().Equal(second, _photo);
    }

    [Fact]
    public async Task ReadLinksAsync_WithoutPhotosOrRequester_DoesNotAskMedia_R_04()
    {
        var media = new FakeMediaApi((_, _) => true);

        (await Adapter(media).ReadLinksAsync("client-sub", [], CancellationToken.None)).Should().BeEmpty();
        (await Adapter(media).ReadLinksAsync("", [_photo], CancellationToken.None)).Should().BeEmpty();

        media.LinkRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadLinksAsync_MediaFails_ShowsNoPhotosInsteadOfAnAddress_R_04()
    {
        var media = new FakeMediaApi((_, _) => true) { Links = _ => throw new IOException("storage down") };

        (await Adapter(media).ReadLinksAsync("client-sub", [_photo], CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ReadLinksAsync_Cancelled_PropagatesTheCancellation_R_04()
    {
        var media = new FakeMediaApi((_, _) => true) { Links = _ => throw new OperationCanceledException() };

        await Assert.ThrowsAsync<OperationCanceledException>(() => Adapter(media).ReadLinksAsync("client-sub", [_photo], CancellationToken.None));
    }

    private static MediaRequestPhotos Adapter(IMediaApi media) => new(media, NullLogger<MediaRequestPhotos>.Instance);
}
