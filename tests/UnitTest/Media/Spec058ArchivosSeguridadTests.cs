using System.Diagnostics.Metrics;
using System.Globalization;
using System.Reflection;
using AwesomeAssertions;
using Knock.Contracts.Moderation;
using Knock.Market.Api.Modules.Media;
using Knock.Market.Api.Modules.Media.Data;
using Knock.Market.Api.Modules.Media.Domain;
using Knock.Market.Api.Modules.Media.Dtos;
using Knock.Market.Api.Modules.Media.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Knock.UnitTest.Media;

/// <summary>Spec 058 (T-06): dueño solo del token, archivo ajeno, <c>archivoId</c> no enumerable y métricas sin datos (R-05, R-12, R-13).</summary>
public sealed class Spec058ArchivosSeguridadTests : IDisposable
{
    private readonly InMemoryMediaFileStore _store = new();
    private readonly ImageProcessor _processor = new(Options.Create(new MediaOptions { ReducedMaxSide = 512, MaxConcurrentProcessing = 1 }));
    private readonly MediaMetrics _metrics = new();
    private readonly MediaService _service;

    public Spec058ArchivosSeguridadTests()
    {
        _service = new MediaService(_store, new InMemoryMediaBlobStore(), _processor, new AllowAll(), TimeProvider.System, Options.Create(new MediaOptions()), _metrics);
    }

    [Fact]
    public async Task Archivo_Ajeno_O_Inexistente_Mismo_Resultado_CA_058_06()
    {
        var ticket = (await Create("owner-a")).Value!;
        var ready = (await _store.FindAsync(ticket.Id, CancellationToken.None))!;
        (await _store.TryReplaceAsync(ready.File.MarkReady(10, 10, DateTimeOffset.UtcNow), ready.Version, CancellationToken.None)).Should().BeTrue();

        var foreign = await _service.ConfirmAsync("owner-b", ticket.Id, CancellationToken.None);
        var missing = await _service.ConfirmAsync("owner-b", Guid.CreateVersion7(), CancellationToken.None);

        foreign.Should().Be(missing);
        foreign.Status.Should().Be(MediaOutcomeStatus.NotFound);
        var api = Spec020bMediaUsoEnlacesTests.NewApi(_service);
        (await api.IsReadyAndOwnedByAsync(ticket.Id, "owner-b", CancellationToken.None)).Should().BeFalse();
        (await api.IsReadyAndOwnedByAsync(Guid.CreateVersion7(), "owner-b", CancellationToken.None)).Should().BeFalse();
        (await api.IsReadyAndOwnedByAsync(ticket.Id, "owner-a", CancellationToken.None)).Should().BeTrue("the real owner still sees it");
    }

    [Fact]
    public async Task Propietario_Solo_Del_Token_CA_058_17()
    {
        typeof(CreateUploadRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name.ToUpperInvariant())
            .Should().NotContain(name => name.Contains("OWNER") || name.Contains("PROPIETARIO") || name == "SUB" || name.Contains("DUENO"));

        var ticket = (await Create("sub-from-token")).Value!;

        (await _store.FindAsync(ticket.Id, CancellationToken.None))!.File.OwnerId.Should().Be("sub-from-token");
    }

    [Fact]
    public void Archivo_Id_Uuid_V7_No_Enumerable_CA_058_18()
    {
        var ids = Enumerable.Range(0, 1000)
            .Select(_ => MediaFile.CreatePending("owner-a", MediaUsage.Request, "image/jpeg", 100, DateTimeOffset.UtcNow).Id)
            .ToList();

        ids.Distinct().Should().HaveCount(1000);
        ids.Should().OnlyContain(id => id.ToString("N", CultureInfo.InvariantCulture).Length == 32
            && id.ToString("N", CultureInfo.InvariantCulture)[12] == '7', "UUID v7 in its 32 hexadecimal form");
    }

    [Fact]
    public async Task Registros_Y_Metricas_Sin_Datos_CA_058_19()
    {
        var measurements = new List<(string Name, int TagCount)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == MediaMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) => measurements.Add((instrument.Name, tags.Length)));
        listener.Start();
        MediaOutcome<UploadTicket> last = default!;
        for (var i = 0; i < 11; i++)
        {
            last = await Create("synthetic-sub");
        }

        measurements.Select(m => m.Name).Distinct().Should().BeEquivalentTo("knock.media.files.created", "knock.media.uploads.rejected_by_limit");
        measurements.Should().OnlyContain(m => m.TagCount == 0);
        var stored = (await _store.FindAsync((await Create("other-sub")).Value!.Id, CancellationToken.None))!.File;
        stored.ToString().Should().NotBeNull();
        last.ToString().Should().NotContain("synthetic-sub").And.NotContain(stored.BlobKey);
        typeof(UploadTicket).GetProperties().Select(p => p.Name).Should().Equal("Id");
    }

    public void Dispose()
    {
        _processor.Dispose();
        _metrics.Dispose();
    }

    private Task<MediaOutcome<UploadTicket>> Create(string owner) =>
        _service.CreateUploadAsync(owner, "solicitud", "image/jpeg", 1000, CancellationToken.None);

    private sealed class AllowAll : IContentModerator
    {
        public Task<ModerationResult> ReviewTextAsync(string text, CancellationToken cancellationToken) => Task.FromResult(new ModerationResult(ModerationOutcome.Allowed));

        public Task<ModerationResult> ReviewImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) => Task.FromResult(new ModerationResult(ModerationOutcome.Allowed));
    }
}
