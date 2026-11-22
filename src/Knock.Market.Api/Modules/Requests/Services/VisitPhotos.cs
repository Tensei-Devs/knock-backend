using Knock.Market.Api.Modules.Media;
using Knock.Market.Api.Modules.Requests.Domain;
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

/// <summary>
/// Adaptador de <see cref="IVisitPhotos"/> sobre <see cref="IMediaApi"/> (spec 032). Solo pasan identificadores de archivo (<c>archivoId</c>, 32 hexadecimales) y el <c>sub</c> del
/// técnico; nunca enlaces ni datos personales. Falla cerrado: un identificador que no sea de archivo, un archivo ajeno o no listo, o un fallo del módulo de archivos dan <c>false</c>.
/// </summary>
internal sealed partial class MediaVisitPhotos(IMediaApi media, ILogger<MediaVisitPhotos> logger) : IVisitPhotos
{
    public async Task<bool> AreReadyForAsync(string ownerId, IReadOnlyList<string> photoIds, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(ownerId) || photoIds.Count == 0)
        {
            return false;
        }

        var files = new List<Guid>(photoIds.Count);
        foreach (var photoId in photoIds)
        {
            if (!VisitCloseRules.TryParsePhotoId(photoId, out var fileId))
            {
                return false;
            }

            files.Add(fileId);
        }

        try
        {
            foreach (var fileId in files)
            {
                if (!await media.IsReadyAndOwnedByAsync(fileId, ownerId, cancellationToken))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogMediaUnavailable(logger, exception.GetType().Name, photoIds.Count);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Media module did not answer while checking visit photos ({ErrorType}, {Count} photos); closing the step.")]
    private static partial void LogMediaUnavailable(ILogger logger, string errorType, int count);
}
