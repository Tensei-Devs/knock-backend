using AwesomeAssertions;
using Knock.Market.Api.Modules.Media;
using Knock.Market.Api.Modules.Requests.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Knock.UnitTest.Requests;

/// <summary>Spec 032 y 050 (R-28): el adaptador de fotos del trabajo sobre <c>IMediaApi</c> pasa solo identificadores y falla cerrado.</summary>
public sealed class Spec032MediaVisitPhotosTests
{
    private const string _owner = "technician-sub";
    private const string _photoA = "018f3c2e7a4b7c3d9e1f2a3b4c5d6e7f";
    private const string _photoB = "018f3c2e7a4b7c3d9e1f2a3b4c5d6e80";

    [Fact]
    public async Task AreReadyForAsync_AllReadyAndOwned_ReturnsTrueAndAsksWithTheOwner_CA_050_49()
    {
        var media = new FakeMediaApi((_, _) => true);

        var ready = await Adapter(media).AreReadyForAsync(_owner, [_photoA, _photoB], CancellationToken.None);

        ready.Should().BeTrue();
        media.Calls.Should().Equal((Guid.ParseExact(_photoA, "N"), _owner), (Guid.ParseExact(_photoB, "N"), _owner));
    }

    [Fact]
    public async Task AreReadyForAsync_OneNotReadyOrNotOwned_ReturnsFalse_CA_050_49()
    {
        var media = new FakeMediaApi((id, _) => id != Guid.ParseExact(_photoB, "N"));

        (await Adapter(media).AreReadyForAsync(_owner, [_photoA, _photoB], CancellationToken.None)).Should().BeFalse();
    }

    [Theory]
    [InlineData("64b7f0c2a1b2c3d4e5f60718")]
    [InlineData("no-es-id")]
    [InlineData("018f3c2e-7a4b-7c3d-9e1f-2a3b4c5d6e7f")]
    public async Task AreReadyForAsync_IdentifierThatIsNotAFileId_ReturnsFalseWithoutAskingMedia_CA_050_49(string photo)
    {
        var media = new FakeMediaApi((_, _) => true);

        (await Adapter(media).AreReadyForAsync(_owner, [_photoA, photo], CancellationToken.None)).Should().BeFalse();
        media.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task AreReadyForAsync_MediaFails_FailsClosed_CA_050_49()
    {
        var media = new FakeMediaApi((_, _) => throw new InvalidOperationException("storage down"));

        (await Adapter(media).AreReadyForAsync(_owner, [_photoA], CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task AreReadyForAsync_Cancelled_PropagatesTheCancellation_CA_050_49()
    {
        var media = new FakeMediaApi((_, _) => throw new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => Adapter(media).AreReadyForAsync(_owner, [_photoA], CancellationToken.None));
    }

    [Fact]
    public async Task AreReadyForAsync_NoOwnerOrNoPhotos_ReturnsFalse_CA_050_49()
    {
        var media = new FakeMediaApi((_, _) => true);

        (await Adapter(media).AreReadyForAsync("", [_photoA], CancellationToken.None)).Should().BeFalse();
        (await Adapter(media).AreReadyForAsync(_owner, [], CancellationToken.None)).Should().BeFalse();
    }

    private static MediaVisitPhotos Adapter(IMediaApi media) => new(media, NullLogger<MediaVisitPhotos>.Instance);
}
