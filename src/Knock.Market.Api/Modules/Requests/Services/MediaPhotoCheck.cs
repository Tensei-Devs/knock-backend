using Knock.Market.Api.Modules.Media;
using Knock.Market.Api.Modules.Requests.Domain;
using Microsoft.Extensions.Logging;

namespace Knock.Market.Api.Modules.Requests.Services;

/// <summary>
/// Comprobación común de fotos privadas sobre <see cref="IMediaApi"/> (specs 020, 032 y 050), compartida por los adaptadores de solicitudes y de visitas. Solo pasan identificadores de
/// archivo (<c>archivoId</c>, 32 hexadecimales) y el <c>sub</c> del dueño; nunca enlaces ni datos personales. Falla cerrado: un identificador que no sea de archivo, un archivo ajeno
/// o no listo, o un fallo del módulo de archivos dan <c>false</c>.
/// </summary>
internal static partial class MediaPhotoCheck
{
    public static async Task<bool> AreReadyAsync(IMediaApi media, ILogger logger, string ownerId, IReadOnlyList<string> photoIds, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(ownerId) || photoIds.Count == 0)
        {
            return false;
        }

        var files = new List<Guid>(photoIds.Count);
        foreach (var photoId in photoIds)
        {
            if (!PhotoIds.TryParse(photoId, out var fileId))
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Media module did not answer while checking photos ({ErrorType}, {Count} photos); closing the step.")]
    private static partial void LogMediaUnavailable(ILogger logger, string errorType, int count);
}
