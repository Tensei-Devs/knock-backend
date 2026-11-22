using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure.Storage.Blobs;
using Knock.Market.Api.Modules.Media.Domain;

namespace Knock.Market.Api.Modules.Media.Data;

/// <summary>Conteos de una corrida del migrador (spec 058, R-17): nunca llevan datos personales ni identificadores.</summary>
internal sealed record ArchivosMigrationReport(int Read, int Inserted, int AlreadyPresent, int DiscardedExpired, int Failed);

/// <summary>
/// Migra los registros JSON antiguos de <c>registros-temporales</c> a la colección <c>archivos</c> (spec 058, T-03, R-08, R-12, R-16, R-17). Es idempotente: el índice único de
/// <c>archivoId</c> hace que un registro ya migrado cuente como «ya presente». Los pendientes cuya ventana de confirmación venció se descartan y su blob de cuarentena se borra.
/// Los fallidos solo se cuentan; el informe no contiene datos personales.
/// </summary>
internal sealed class ArchivosMigrator(IMediaFileStore store)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>Lee los blobs <c>*.json</c> del contenedor de registros antiguo; no modifica nada.</summary>
    public static IAsyncEnumerable<BinaryData> ReadLegacyAsync(BlobContainerClient records, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        return ReadBlobsAsync(records, cancellationToken);
    }

    private static async IAsyncEnumerable<BinaryData> ReadBlobsAsync(BlobContainerClient records, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in records.GetBlobsAsync(cancellationToken: cancellationToken).Where(blob => blob.Name.EndsWith(".json", StringComparison.Ordinal)))
        {
            var content = await records.GetBlobClient(item.Name).DownloadContentAsync(cancellationToken);
            yield return content.Value.Content;
        }
    }

    public async Task<ArchivosMigrationReport> MigrateAsync(
        IAsyncEnumerable<BinaryData> legacyRecords,
        Func<MediaFile, CancellationToken, Task> discardQuarantineBlob,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(legacyRecords);
        ArgumentNullException.ThrowIfNull(discardQuarantineBlob);
        int read = 0, inserted = 0, present = 0, discarded = 0, failed = 0;
        await foreach (var raw in legacyRecords.WithCancellation(cancellationToken))
        {
            read++;
            MediaFile file;
            try
            {
                file = Translate(raw);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            {
                failed++;
                continue;
            }

            if (file.State == MediaFileState.Pending && file.ConfirmBefore <= now)
            {
                await discardQuarantineBlob(file, cancellationToken);
                discarded++;
                continue;
            }

            try
            {
                await store.AddAsync(file, cancellationToken);
                inserted++;
            }
            catch (InvalidOperationException)
            {
                present++;
            }
        }

        return new ArchivosMigrationReport(read, inserted, present, discarded, failed);
    }

    private static MediaFile Translate(BinaryData raw)
    {
        var record = raw.ToObjectFromJson<LegacyRecord>(_json) ?? throw new InvalidOperationException("The legacy record is empty.");
        var usage = MediaRules.ParseUsage(record.Uso) ?? throw new InvalidOperationException("Unknown media usage in the legacy record.");
        var state = MongoMediaFileStore.ParseState(record.Estado);
        return new MediaFile(record.Id, record.BlobKey, record.PropietarioId, usage, state, record.ContentType, record.TamanoBytes, record.CreadoEn)
        {
            OriginalBlob = record.BlobOriginal,
            ReducedBlob = record.BlobReducido,
            Width = record.Ancho,
            Height = record.Alto,
            ConfirmedAt = record.ConfirmadoEn,
        };
    }

    /// <summary>Forma del JSON antiguo (snake_case, nombres en español), igual a la que escribía el almacén retirado.</summary>
    private sealed record LegacyRecord(
        Guid Id,
        string BlobKey,
        string PropietarioId,
        string Uso,
        string Estado,
        string ContentType,
        long TamanoBytes,
        string? BlobOriginal,
        string? BlobReducido,
        int? Ancho,
        int? Alto,
        DateTimeOffset CreadoEn,
        DateTimeOffset? ConfirmadoEn);
}
