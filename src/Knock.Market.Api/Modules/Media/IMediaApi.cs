namespace Knock.Market.Api.Modules.Media;

/// <summary>
/// Public module contract: the only way for other modules or services to use private photos.
/// The <c>use</c> argument is the contract value of the file's <c>uso</c> (spec 058): <c>solicitud</c> or <c>trabajo</c>; any other value is rejected.
/// </summary>
public interface IMediaApi
{
    /// <summary>Whether the file exists, is Ready and its owner is <paramref name="ownerId"/> (the JWT <c>sub</c>).</summary>
    Task<bool> IsReadyAndOwnedByAsync(Guid fileId, string ownerId, CancellationToken cancellationToken);

    /// <summary>Like <see cref="IsReadyAndOwnedByAsync"/> and also requires the file to have <paramref name="use"/> as its <c>uso</c> (spec 020 CA-18, spec 050 R-28). It never says which condition failed; an unknown <paramref name="use"/> gives <see langword="false"/>.</summary>
    Task<bool> IsReadyOwnedAndUsedForAsync(Guid fileId, string ownerId, string use, CancellationToken cancellationToken);

    /// <summary>
    /// Read links (5 minutes, reduced version) for files that are Ready and have <paramref name="use"/> (<c>solicitud</c> or <c>trabajo</c>). The caller must already have authorized <paramref name="requesterSub"/>; the access
    /// is logged with an HMAC of the <c>sub</c> (spec 032, R-16). Files that do not exist, are not Ready or have another <c>uso</c> are left out; the order of <paramref name="fileIds"/> is kept.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="use"/> is not <c>solicitud</c> or <c>trabajo</c>.</exception>
    Task<IReadOnlyList<(Guid FileId, Uri Url, DateTimeOffset ExpiresAt)>> CreateReadLinksAsync(IReadOnlyList<Guid> fileIds, string use, string requesterSub, CancellationToken cancellationToken);
}
