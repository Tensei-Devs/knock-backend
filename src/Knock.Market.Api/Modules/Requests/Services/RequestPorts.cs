using Knock.Contracts.Moderation;
using Knock.Market.Api.Modules.Media;
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

    /// <summary>Enlaces de lectura de 5 minutos (versión reducida) de fotos ya validadas.</summary>
    Task<IReadOnlyList<RequestPhoto>> ReadLinksAsync(IReadOnlyList<string> photoIds, CancellationToken cancellationToken);
}

/// <summary>Sin módulo de archivos: ninguna foto es válida, así que no se crea ninguna solicitud. Cierra el paso en lugar de aceptar fotos que nadie confirmó.</summary>
internal sealed class UnavailableRequestPhotos : IRequestPhotos
{
    public Task<bool> AreReadyForAsync(string ownerId, IReadOnlyList<string> photoIds, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<IReadOnlyList<RequestPhoto>> ReadLinksAsync(IReadOnlyList<string> photoIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RequestPhoto>>([]);
}

/// <summary>
/// Adaptador de <see cref="IRequestPhotos"/> sobre <see cref="IMediaApi"/> (spec 020 R-04 y spec 032): la comprobación es la de <see cref="MediaPhotoCheck"/>. <c>IMediaApi</c> aún no emite
/// enlaces de lectura (spec 032, OI-3), así que <see cref="ReadLinksAsync"/> no devuelve fotos hasta entonces: nunca se inventa ni se expone una dirección.
/// </summary>
internal sealed class MediaRequestPhotos(IMediaApi media, ILogger<MediaRequestPhotos> logger) : IRequestPhotos
{
    public Task<bool> AreReadyForAsync(string ownerId, IReadOnlyList<string> photoIds, CancellationToken cancellationToken) =>
        MediaPhotoCheck.AreReadyAsync(media, logger, ownerId, photoIds, cancellationToken);

    public Task<IReadOnlyList<RequestPhoto>> ReadLinksAsync(IReadOnlyList<string> photoIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RequestPhoto>>([]);
}

/// <summary>Sin proveedor de moderación configurado: falla cerrado (<c>Unavailable</c>), así que nada se publica sin revisión (spec 013, R-05 y R-06).</summary>
internal sealed class UnavailableContentModerator : IContentModerator
{
    public Task<ModerationResult> ReviewTextAsync(string text, CancellationToken cancellationToken) =>
        Task.FromResult(new ModerationResult(ModerationOutcome.Unavailable));

    public Task<ModerationResult> ReviewImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) =>
        Task.FromResult(new ModerationResult(ModerationOutcome.Unavailable));
}
