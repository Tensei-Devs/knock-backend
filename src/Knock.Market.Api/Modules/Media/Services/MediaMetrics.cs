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
    }

    public Counter<long> FilesCreated { get; }

    public Counter<long> UploadsRejectedByLimit { get; }

    public void Dispose() => _meter.Dispose();
}
