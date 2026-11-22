using Knock.Market.Api.Modules.Media.Domain;

namespace Knock.Market.Api.Modules.Media.Data;

/// <summary>Stored record with its version (for optimistic concurrency).</summary>
internal sealed record StoredMediaFile(MediaFile File, string Version);

/// <summary>Result of <see cref="IMediaFileStore.AddPendingWithinLimitAsync"/>: whether the file was kept and, if not, when the oldest valid pending file stops counting.</summary>
internal sealed record PendingAddResult(bool Added, DateTimeOffset? RetryAt);

/// <summary>
/// Persistence of each file record. Internal to the module; other modules use <c>IMediaApi</c>.
/// </summary>
internal interface IMediaFileStore
{
    /// <exception cref="InvalidOperationException">A record with the same id already exists.</exception>
    Task AddAsync(MediaFile file, CancellationToken cancellationToken);

    Task<StoredMediaFile?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a pending file under the per-owner cap (spec 058, R-09/R-10), counting in the database so the result is the same across replicas: inserts first, counts the owner's
    /// pending files still inside their confirmation window (the new one included) and, if the cap is exceeded, removes the file it just inserted (only while still pending).
    /// Concurrent requests may reject one too many but never leave more than <paramref name="limit"/> pending files.
    /// </summary>
    /// <exception cref="InvalidOperationException">A record with the same id already exists.</exception>
    Task<PendingAddResult> AddPendingWithinLimitAsync(MediaFile file, int limit, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Replaces the record only if still at <paramref name="expectedVersion"/>; false if another request changed it first.</summary>
    Task<bool> TryReplaceAsync(MediaFile file, string expectedVersion, CancellationToken cancellationToken);
}
