using Knock.Contracts.Moderation;
using Knock.Market.Api.Modules.Media.Data;
using Knock.Market.Api.Modules.Media.Domain;
using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Modules.Media.Services;

/// <summary>Upload ticket: the pending file id. The signed upload link is OI-3.</summary>
internal sealed record UploadTicket(Guid Id);

/// <summary>Pending file with its signed upload link (spec 062, R-01): the id (<c>archivoId</c>), the link and the time limit to confirm.</summary>
internal sealed record IssuedUpload(Guid Id, EnlaceSubida Link, DateTimeOffset ConfirmBefore);

/// <summary>Confirmed file: final state and dimensions (no URLs until OI-3).</summary>
internal sealed record ConfirmedFile(Guid Id, string State, int Width, int Height, int Bytes = 0);

/// <summary>
/// Upload, confirmation and ownership of private photos. The client uploads to quarantine with the OI-3 signed link;
/// on confirmation the content is validated by signature and decode, cleaned, moderated and only then moved to the
/// final container. The owner is always the JWT <c>sub</c>; anyone else gets the same 404 as a missing file.
/// </summary>
internal sealed class MediaService(
    IMediaFileStore store,
    IMediaBlobStore blobs,
    ImageProcessor processor,
    IContentModerator moderator,
    TimeProvider timeProvider,
    IOptions<MediaOptions>? options = null,
    MediaMetrics? metrics = null)
{
    private readonly int _maxPendingUploads = options?.Value.MaxPendingUploads ?? MediaRules.DefaultMaxPendingUploads;

    public async Task<MediaOutcome<UploadTicket>> CreateUploadAsync(
        string ownerId, string usage, string contentType, long sizeBytes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerId);
        var parsedUsage = MediaRules.ParseUsage(usage);
        if (parsedUsage is null || !MediaRules.IsAllowedContentType(contentType) || !MediaRules.IsAllowedSize(sizeBytes))
        {
            return MediaOutcome<UploadTicket>.Unprocessable(MediaRejection.InvalidFile);
        }

        await blobs.EnsureContainersAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var file = MediaFile.CreatePending(ownerId, parsedUsage.Value, contentType, sizeBytes, now);
        var added = await store.AddPendingWithinLimitAsync(file, _maxPendingUploads, now, cancellationToken);
        if (!added.Added)
        {
            metrics?.UploadsRejectedByLimit.Add(1);
            return MediaOutcome<UploadTicket>.LimitExceeded(RetryAfterSeconds(added.RetryAt, now));
        }

        metrics?.FilesCreated.Add(1);
        return MediaOutcome<UploadTicket>.Ok(new UploadTicket(file.Id));
    }

    /// <summary>
    /// Creates the pending file and issues its signed upload link to the private quarantine (spec 062, R-01 and R-03). A storage failure after the file was created leaves a pending
    /// document that expires by itself (30 minutes) and counts toward the cap only until then.
    /// </summary>
    public async Task<MediaOutcome<IssuedUpload>> RequestUploadAsync(
        string ownerId, string usage, string contentType, long sizeBytes, CancellationToken cancellationToken)
    {
        var created = await CreateUploadAsync(ownerId, usage, contentType, sizeBytes, cancellationToken);
        if (created.Status != MediaOutcomeStatus.Success)
        {
            return new MediaOutcome<IssuedUpload>(created.Status, Detail: created.Detail, RetryAfterSeconds: created.RetryAfterSeconds);
        }

        var stored = await store.FindAsync(created.Value!.Id, cancellationToken);
        if (stored is null)
        {
            return MediaOutcome<IssuedUpload>.Unavailable();
        }

        try
        {
            var link = await blobs.CreateUploadLinkAsync(stored.File.QuarantineBlob, contentType, cancellationToken);
            return MediaOutcome<IssuedUpload>.Ok(new IssuedUpload(stored.File.Id, link, stored.File.CreatedAt + MediaRules.ConfirmationWindow));
        }
        catch (IOException)
        {
            return MediaOutcome<IssuedUpload>.Unavailable();
        }
    }

    /// <summary>Seconds until the oldest valid pending file stops counting, between 1 and the confirmation window (1 800 s) (spec 058, R-11).</summary>
    private static int RetryAfterSeconds(DateTimeOffset? retryAt, DateTimeOffset now)
    {
        var window = (int)MediaRules.ConfirmationWindow.TotalSeconds;
        return retryAt is null ? window : Math.Clamp((int)Math.Ceiling((retryAt.Value - now).TotalSeconds), 1, window);
    }

    public async Task<MediaOutcome<ConfirmedFile>> ConfirmAsync(string ownerId, Guid id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerId);
        var stored = await store.FindAsync(id, cancellationToken);
        if (stored is null || !string.Equals(stored.File.OwnerId, ownerId, StringComparison.Ordinal))
        {
            return MediaOutcome<ConfirmedFile>.NotFound();
        }

        if (stored.File.State != MediaFileState.Pending)
        {
            return MediaOutcome<ConfirmedFile>.Conflict("The file was already confirmed.");
        }

        await blobs.EnsureContainersAsync(cancellationToken);
        var uploaded = await blobs.GetUploadedAsync(stored.File.QuarantineBlob, cancellationToken);
        if (uploaded is null)
        {
            return MediaOutcome<ConfirmedFile>.Unprocessable(MediaRejection.NotUploaded);
        }

        var image = await ReadImageAsync(stored.File, uploaded, cancellationToken);
        if (image.Status != MediaOutcomeStatus.Success)
        {
            return await RejectAsync(stored, image.Detail ?? MediaRejection.InvalidFile, cancellationToken);
        }

        ModerationResult review;
        try
        {
            review = await moderator.ReviewImageAsync(image.Value!.Original, cancellationToken);
        }
        catch (TimeoutException)
        {
            return MediaOutcome<ConfirmedFile>.Unavailable(MediaRejection.ModerationUnavailable);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return MediaOutcome<ConfirmedFile>.Unavailable(MediaRejection.ModerationUnavailable);
        }

        if (review.Outcome == ModerationOutcome.Unavailable)
        {
            return MediaOutcome<ConfirmedFile>.Unavailable(MediaRejection.ModerationUnavailable);
        }
        if (review.Outcome != ModerationOutcome.Allowed)
        {
            return await RejectAsync(stored, MediaRejection.ModerationRejected, cancellationToken);
        }

        return await AcceptAsync(stored, image.Value, cancellationToken);
    }

    /// <summary>True only for a Ready file owned by <paramref name="ownerId"/> (the JWT <c>sub</c>).</summary>
    public async Task<bool> IsReadyAndOwnedByAsync(Guid id, string ownerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerId);
        var stored = await store.FindAsync(id, cancellationToken);
        return stored is not null && stored.File.IsReadyFor(ownerId);
    }

    /// <summary>True only for a Ready file owned by <paramref name="ownerId"/> whose <c>uso</c> is <paramref name="usage"/> (spec 020 CA-18).</summary>
    public async Task<bool> IsReadyOwnedAndUsedForAsync(Guid id, string ownerId, MediaUsage usage, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerId);
        var stored = await store.FindAsync(id, cancellationToken);
        return stored is not null && stored.File.Usage == usage && stored.File.IsReadyFor(ownerId);
    }

    /// <summary>The file when it is Ready, has <paramref name="usage"/> and has a reduced blob to link; otherwise <see langword="null"/>. Ownership is the caller's decision (it already authorized the reader).</summary>
    public async Task<MediaFile?> FindReadableAsync(Guid id, MediaUsage usage, CancellationToken cancellationToken)
    {
        var stored = await store.FindAsync(id, cancellationToken);
        return stored is { File: { State: MediaFileState.Ready, ReducedBlob: not null } file } && file.Usage == usage ? file : null;
    }

    /// <summary>
    /// Real size is checked before downloading, and the download only returns the consulted version (ETag) and at
    /// most 5 MB + 1 byte. Client bytes are never stored as-is.
    /// </summary>
    private async Task<MediaOutcome<ProcessedImage>> ReadImageAsync(MediaFile file, UploadedBlob uploaded, CancellationToken cancellationToken)
    {
        if (!MediaRules.IsAllowedSize(uploaded.SizeBytes))
        {
            return MediaOutcome<ProcessedImage>.Unprocessable(MediaRejection.InvalidFile);
        }

        var content = await blobs.DownloadQuarantineAsync(file.QuarantineBlob, uploaded, cancellationToken);
        if (content is not { } bytes || bytes.Length != uploaded.SizeBytes)
        {
            return MediaOutcome<ProcessedImage>.Unprocessable(MediaRejection.ChangedDuringConfirmation);
        }

        return await processor.ProcessAsync(new ArraySegment<byte>(bytes), file.Usage, cancellationToken);
    }

    private async Task<MediaOutcome<ConfirmedFile>> RejectAsync(StoredMediaFile stored, string detail, CancellationToken cancellationToken)
    {
        await blobs.DeleteQuarantineAsync(stored.File.QuarantineBlob, cancellationToken);
        var rejected = stored.File.MarkRejected(timeProvider.GetUtcNow());
        return await store.TryReplaceAsync(rejected, stored.Version, cancellationToken)
            ? MediaOutcome<ConfirmedFile>.Unprocessable(detail)
            : MediaOutcome<ConfirmedFile>.Conflict("The file was confirmed by another request.");
    }

    private async Task<MediaOutcome<ConfirmedFile>> AcceptAsync(StoredMediaFile stored, ProcessedImage image, CancellationToken cancellationToken)
    {
        var ready = stored.File.MarkReady(image.Width, image.Height, timeProvider.GetUtcNow());
        await blobs.UploadFileAsync(ready.OriginalBlob!, image.Original, image.Format, cancellationToken);
        await blobs.UploadFileAsync(ready.ReducedBlob!, image.Reduced, image.Format, cancellationToken);
        await blobs.DeleteQuarantineAsync(ready.QuarantineBlob, cancellationToken);
        return await store.TryReplaceAsync(ready, stored.Version, cancellationToken)
            ? MediaOutcome<ConfirmedFile>.Ok(new ConfirmedFile(ready.Id, ready.State.ToContractValue(), image.Width, image.Height, image.Original.Length))
            : MediaOutcome<ConfirmedFile>.Conflict("The file was confirmed by another request.");
    }
}
