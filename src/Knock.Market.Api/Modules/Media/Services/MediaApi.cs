using Knock.Market.Api.Modules.Media.Data;
using Knock.Market.Api.Modules.Media.Domain;
using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Modules.Media.Services;

/// <summary>Public module facade over the internal media service and the private read links.</summary>
internal sealed class MediaApi(MediaService media, IEnlacesPrivados links, IOptions<MediaOptions> options) : IMediaApi
{
    public Task<bool> IsReadyAndOwnedByAsync(Guid fileId, string ownerId, CancellationToken cancellationToken) =>
        media.IsReadyAndOwnedByAsync(fileId, ownerId, cancellationToken);

    public Task<bool> IsReadyOwnedAndUsedForAsync(Guid fileId, string ownerId, string use, CancellationToken cancellationToken) =>
        MediaRules.ParseUsage(use) is { } usage and not MediaUsage.Profile
            ? media.IsReadyOwnedAndUsedForAsync(fileId, ownerId, usage, cancellationToken)
            : Task.FromResult(false);

    public async Task<IReadOnlyList<(Guid FileId, Uri Url, DateTimeOffset ExpiresAt)>> CreateReadLinksAsync(IReadOnlyList<Guid> fileIds, string use, string requesterSub, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileIds);
        ArgumentException.ThrowIfNullOrEmpty(requesterSub);
        var usage = MediaRules.ParseUsage(use);
        var reason = usage switch
        {
            MediaUsage.Request => MotivoAcceso.FotoSolicitud,
            MediaUsage.Job => MotivoAcceso.FotoTrabajo,
            _ => throw new ArgumentOutOfRangeException(nameof(use)),
        };
        var result = new List<(Guid FileId, Uri Url, DateTimeOffset ExpiresAt)>(fileIds.Count);
        foreach (var fileId in fileIds)
        {
            var file = await media.FindReadableAsync(fileId, usage.Value, cancellationToken);
            if (file is null)
            {
                continue;
            }

            var link = await links.CrearLecturaAsync(new BlobPrivado(options.Value.FilesContainer, file.ReducedBlob!), reason, requesterSub, cancellationToken);
            result.Add((file.Id, link.Url, link.VenceEn));
        }

        return result;
    }
}
