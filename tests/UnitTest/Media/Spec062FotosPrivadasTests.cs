using AwesomeAssertions;
using Knock.Contracts.Moderation;
using Knock.Market.Api.Modules.Media.Data;
using Knock.Market.Api.Modules.Media.Domain;
using Knock.Market.Api.Modules.Media.Dtos;
using Knock.Market.Api.Modules.Media.Services;
using Knock.Market.Api.Modules.Media.Validators;
using Microsoft.Extensions.Options;
using Xunit;

namespace Knock.UnitTest.Media;

/// <summary>Spec 062, flujo privado de fotos de solicitud: emisión del enlace de subida, tope de pendientes y propósito aceptado por el validador.</summary>
public sealed class Spec062FotosPrivadasTests
{
    private const string _owner = "persona-1";

    private static ImageProcessor CreateProcessor() => new(Options.Create(new MediaOptions()));

    private static MediaService CreateService(ImageProcessor processor, InMemoryMediaFileStore files, int maxPending = 10) =>
        new(files, new InMemoryMediaBlobStore(), processor, new AllowAll(), TimeProvider.System, Options.Create(new MediaOptions { MaxPendingUploads = maxPending }));

    [Fact]
    public async Task RequestUpload_Solicitud_ReturnsAnId32HexAndAWriteLink_CA_062_01()
    {
        using var processor = CreateProcessor();
        var files = new InMemoryMediaFileStore();
        var service = CreateService(processor, files);

        var issued = await service.RequestUploadAsync(_owner, "solicitud", "image/jpeg", 1_000_000, CancellationToken.None);

        issued.Status.Should().Be(MediaOutcomeStatus.Success);
        var id = issued.Value!.Id.ToString("N");
        id.Should().HaveLength(32).And.MatchRegex("^[0-9a-f]{32}$");
        issued.Value.Link.Url.Scheme.Should().Be("https");
        issued.Value.Link.Encabezados.Should().ContainKey("Content-Type").WhoseValue.Should().Be("image/jpeg");
        issued.Value.ConfirmBefore.Should().BeAfter(DateTimeOffset.UtcNow);
        var stored = await files.FindAsync(issued.Value.Id, CancellationToken.None);
        stored!.File.Usage.Should().Be(MediaUsage.Request);
        stored.File.OwnerId.Should().Be(_owner);
        stored.File.State.Should().Be(MediaFileState.Pending);
    }

    [Fact]
    public async Task RequestUpload_PastTheCap_ReturnsLimitExceededWithRetryAfter_CA_062_04()
    {
        using var processor = CreateProcessor();
        var service = CreateService(processor, new InMemoryMediaFileStore(), maxPending: 2);
        (await service.RequestUploadAsync(_owner, "solicitud", "image/jpeg", 1000, CancellationToken.None)).Status.Should().Be(MediaOutcomeStatus.Success);
        (await service.RequestUploadAsync(_owner, "solicitud", "image/png", 1000, CancellationToken.None)).Status.Should().Be(MediaOutcomeStatus.Success);

        var third = await service.RequestUploadAsync(_owner, "solicitud", "image/jpeg", 1000, CancellationToken.None);

        third.Status.Should().Be(MediaOutcomeStatus.LimitExceeded);
        third.RetryAfterSeconds.Should().BeGreaterThan(0);
        third.Value.Should().BeNull("no se emite ningún enlace cuando se alcanzó el tope");
    }

    [Theory]
    [InlineData("image/svg+xml", 1000L)]
    [InlineData("image/jpeg", 5_242_881L)]
    public async Task RequestUpload_InvalidFile_IsUnprocessableAndIssuesNoLink_CA_062_05(string contentType, long size)
    {
        using var processor = CreateProcessor();
        var service = CreateService(processor, new InMemoryMediaFileStore());

        var issued = await service.RequestUploadAsync(_owner, "solicitud", contentType, size, CancellationToken.None);

        issued.Status.Should().Be(MediaOutcomeStatus.Unprocessable);
        issued.Value.Should().BeNull();
    }

    [Theory]
    [InlineData("solicitud", true)]
    [InlineData("perfil", true)]
    [InlineData("trabajo", true)]
    [InlineData("documento", false)]
    public async Task Validator_AcceptsTheThreePurposes_CA_062_05(string purpose, bool valid)
    {
        var validator = new PedirSubidaImagenRequestValidator();

        var result = await validator.ValidateAsync(new PedirSubidaImagenRequest(purpose, "image/jpeg", 1000), CancellationToken.None);

        result.IsValid.Should().Be(valid);
    }

    private sealed class AllowAll : IContentModerator
    {
        public Task<ModerationResult> ReviewTextAsync(string text, CancellationToken cancellationToken) => Task.FromResult(new ModerationResult(ModerationOutcome.Allowed));

        public Task<ModerationResult> ReviewImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) => Task.FromResult(new ModerationResult(ModerationOutcome.Allowed));
    }
}
