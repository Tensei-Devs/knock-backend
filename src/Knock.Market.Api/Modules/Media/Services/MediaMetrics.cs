using System.Diagnostics.Metrics;

namespace Knock.Market.Api.Modules.Media.Services;

/// <summary>
/// Contadores del módulo (spec 058, R-12): solo conteos, sin identificadores de persona, nombres de blob ni contenido de documentos.
/// </summary>
internal sealed class MediaMetrics : IDisposable
{
    public const string MeterName = "Knock.Media";

    private readonly Meter _meter = new(MeterName);

    public MediaMetrics()
    {
        FilesCreated = _meter.CreateCounter<long>("knock.media.files.created");
        UploadsRejectedByLimit = _meter.CreateCounter<long>("knock.media.uploads.rejected_by_limit");
        MigrationMigrated = _meter.CreateCounter<long>("knock.media.migration.migrated");
        MigrationAlreadyPresent = _meter.CreateCounter<long>("knock.media.migration.already_present");
        MigrationExpiredDiscarded = _meter.CreateCounter<long>("knock.media.migration.expired_discarded");
        MigrationFailed = _meter.CreateCounter<long>("knock.media.migration.failed");
        MigrationDuration = _meter.CreateHistogram<double>("knock.media.migration.duration", "s");
    }

    public Counter<long> FilesCreated { get; }

    public Counter<long> UploadsRejectedByLimit { get; }

    public Counter<long> MigrationMigrated { get; }

    public Counter<long> MigrationAlreadyPresent { get; }

    public Counter<long> MigrationExpiredDiscarded { get; }

    public Counter<long> MigrationFailed { get; }

    public Histogram<double> MigrationDuration { get; }

    public void Dispose() => _meter.Dispose();
}
