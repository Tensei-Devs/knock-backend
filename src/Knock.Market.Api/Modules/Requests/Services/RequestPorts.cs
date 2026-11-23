using Knock.Contracts.Moderation;
using Knock.Market.Api.Modules.Media;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;
using Microsoft.Extensions.Logging;

namespace Knock.Market.Api.Modules.Requests.Services;

/// <summary>
/// Fotos privadas de una solicitud (spec 020, R-04). <see cref="MediaRequestPhotos"/> adapta <c>IMediaApi</c> a esta interfaz; <see cref="UnavailableRequestPhotos"/> falla cerrado
/// para quien registre el módulo sin archivos y las pruebas usan dobles.
/// </summary>
internal interface IRequestPhotos
{
    /// <summary>Verdadero solo si todas las fotos existen, son de <paramref name="ownerId"/>, tienen <c>uso = solicitud</c> y están <c>listo</c>. No dice cuál condición falló.</summary>
    Task<bool> AreReadyForAsync(string ownerId, IReadOnlyList<string> photoIds, CancellationToken cancellationToken);

    /// <summary>Enlaces de lectura de 5 minutos (versión reducida) de fotos ya validadas, pedidos por <paramref name="requesterSub"/> (el <c>sub</c> de quien ya está autorizado a ver la solicitud; queda en el registro de accesos).</summary>
    Task<IReadOnlyList<RequestPhoto>> ReadLinksAsync(string requesterSub, IReadOnlyList<string> photoIds, CancellationToken cancellationToken);
}

/// <summary>Sin módulo de archivos: ninguna foto es válida, así que no se crea ninguna solicitud. Cierra el paso en lugar de aceptar fotos que nadie confirmó.</summary>
internal sealed class UnavailableRequestPhotos : IRequestPhotos
{
    public Task<bool> AreReadyForAsync(string ownerId, IReadOnlyList<string> photoIds, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<IReadOnlyList<RequestPhoto>> ReadLinksAsync(string requesterSub, IReadOnlyList<string> photoIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RequestPhoto>>([]);
}

/// <summary>
/// Adaptador de <see cref="IRequestPhotos"/> sobre <see cref="IMediaApi"/> (spec 020 R-04 y spec 032): la comprobación es la de <see cref="MediaPhotoCheck"/> con <c>uso = solicitud</c>, y los enlaces de
/// lectura son los de 5 minutos del módulo de archivos. Si el módulo no responde, la solicitud se muestra sin fotos (cierra en lugar de inventar una dirección) y no se registra nada de las personas.
/// </summary>
internal sealed partial class MediaRequestPhotos(IMediaApi media, ILogger<MediaRequestPhotos> logger) : IRequestPhotos
{
    public Task<bool> AreReadyForAsync(string ownerId, IReadOnlyList<string> photoIds, CancellationToken cancellationToken) =>
        MediaPhotoCheck.AreReadyAsync(media, logger, PhotoUses.Request, ownerId, photoIds, cancellationToken);

    public async Task<IReadOnlyList<RequestPhoto>> ReadLinksAsync(string requesterSub, IReadOnlyList<string> photoIds, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(requesterSub) || photoIds.Count == 0)
        {
            return [];
        }

        var files = new List<Guid>(photoIds.Count);
        foreach (var photoId in photoIds)
        {
            if (PhotoIds.TryParse(photoId, out var fileId))
            {
                files.Add(fileId);
            }
        }

        try
        {
            var links = await media.CreateReadLinksAsync(files, PhotoUses.Request, requesterSub, cancellationToken);
            return [.. links.Select(link => new RequestPhoto(PhotoIds.Canonical(link.FileId), link.Url.AbsoluteUri, link.ExpiresAt))];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogLinksUnavailable(logger, exception.GetType().Name, files.Count);
            return [];
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Media module could not issue read links ({ErrorType}, {Count} photos); the request is shown without photos.")]
    private static partial void LogLinksUnavailable(ILogger logger, string errorType, int count);
}

/// <summary>Sin proveedor de moderación configurado: falla cerrado (<c>Unavailable</c>), así que nada se publica sin revisión (spec 013, R-05 y R-06).</summary>
internal sealed class UnavailableContentModerator : IContentModerator
{
    public Task<ModerationResult> ReviewTextAsync(string text, CancellationToken cancellationToken) =>
        Task.FromResult(new ModerationResult(ModerationOutcome.Unavailable));

    public Task<ModerationResult> ReviewImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) =>
        Task.FromResult(new ModerationResult(ModerationOutcome.Unavailable));
}
