using Knock.Market.Api.Modules.Media;
using Microsoft.Extensions.Logging;

namespace Knock.Market.Api.Modules.Requests.Services;

/// <summary>
/// Fotos privadas del trabajo terminado (spec 050, R-28, y spec 032). Una sola clase de este módulo, <see cref="MediaVisitPhotos"/>, adapta <c>IMediaApi</c> (módulo de archivos)
/// a esta interfaz. <see cref="UnavailableVisitPhotos"/> falla cerrado y queda para quien registre el módulo sin archivos; las pruebas usan dobles.
/// </summary>
internal interface IVisitPhotos
{
    /// <summary>Verdadero solo si todas las fotos existen, son de <paramref name="ownerId"/> (el técnico) y están <c>listo</c>. No dice cuál condición falló.</summary>
    Task<bool> AreReadyForAsync(string ownerId, IReadOnlyList<string> photoIds, CancellationToken cancellationToken);
}

/// <summary>Sin módulo de archivos todavía: ninguna foto es válida, así que ninguna visita se termina. Cierra el paso en lugar de aceptar fotos que nadie confirmó.</summary>
internal sealed class UnavailableVisitPhotos : IVisitPhotos
{
    public Task<bool> AreReadyForAsync(string ownerId, IReadOnlyList<string> photoIds, CancellationToken cancellationToken) => Task.FromResult(false);
}

/// <summary>Adaptador de <see cref="IVisitPhotos"/> sobre <see cref="IMediaApi"/> (spec 032); la comprobación es la de <see cref="MediaPhotoCheck"/>.</summary>
internal sealed class MediaVisitPhotos(IMediaApi media, ILogger<MediaVisitPhotos> logger) : IVisitPhotos
{
    public Task<bool> AreReadyForAsync(string ownerId, IReadOnlyList<string> photoIds, CancellationToken cancellationToken) =>
        MediaPhotoCheck.AreReadyAsync(media, logger, ownerId, photoIds, cancellationToken);
}
