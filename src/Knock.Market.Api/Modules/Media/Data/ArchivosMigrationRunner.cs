using Azure.Storage.Blobs;
using Knock.Market.Api.Modules.Media.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Knock.Market.Api.Modules.Media.Data;

/// <summary>
/// Ejecuta el migrador sobre un contenedor de registros antiguo (spec 058, T-03, R-08, R-16, R-17): si el contenedor no existe termina con cero y no lo crea (CA-14); si existe, migra, descarta
/// los pendientes vencidos con el borrado de su blob de cuarentena y reporta los fallidos solo por el nombre del blob (CA-15). Nunca borra ni modifica el contenedor de origen.
/// </summary>
internal sealed partial class ArchivosMigrationRunner(IMediaFileStore store, IMediaBlobStore blobs, TimeProvider clock, MediaMetrics? metrics = null, ILogger<ArchivosMigrationRunner>? logger = null)
{
    private readonly ILogger _logger = logger ?? NullLogger<ArchivosMigrationRunner>.Instance;

    public async Task<ArchivosMigrationReport> RunAsync(BlobServiceClient service, string sourceContainer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceContainer);
        var records = service.GetBlobContainerClient(sourceContainer);
        if (!(await records.ExistsAsync(cancellationToken)).Value)
        {
            return new ArchivosMigrationReport(0, 0, 0, 0, 0);
        }

        var report = await new ArchivosMigrator(store, metrics).MigrateNamedAsync(
            ArchivosMigrator.ReadLegacyNamedAsync(records, cancellationToken),
            (file, token) => blobs.DeleteQuarantineAsync(file.QuarantineBlob, token),
            clock.GetUtcNow(),
            name => FailedRecord(_logger, name),
            cancellationToken);
        var removable = SourceCanBeRemoved(report);
        Finished(_logger, report.Read, report.Inserted, report.AlreadyPresent, report.DiscardedExpired, report.Failed, removable);
        return report;
    }

    /// <summary>R-16: el contenedor de origen solo se puede borrar si no hubo fallidos y cada registro leído quedó migrado, ya presente o descartado por vencido.</summary>
    public static bool SourceCanBeRemoved(ArchivosMigrationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return report.Failed == 0 && report.Read == report.Inserted + report.AlreadyPresent + report.DiscardedExpired;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Legacy record could not be migrated: {BlobName}")]
    private static partial void FailedRecord(ILogger logger, string blobName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Archivos migration finished: read {Read}, migrated {Migrated}, already present {AlreadyPresent}, expired discarded {Discarded}, failed {Failed}, source removable {Removable}")]
    private static partial void Finished(ILogger logger, int read, int migrated, int alreadyPresent, int discarded, int failed, bool removable);
}
