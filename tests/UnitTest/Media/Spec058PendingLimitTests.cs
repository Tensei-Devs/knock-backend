using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Knock.Contracts.Moderation;
using Knock.Market.Api.Modules.Media.Data;
using Knock.Market.Api.Modules.Media.Domain;
using Knock.Market.Api.Modules.Media.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Knock.UnitTest.Media;

/// <summary>Spec 058 (T-04): tope de subidas pendientes por persona (R-09 a R-12), con el almacén en memoria y un reloj fijo.</summary>
public sealed class Spec058PendingLimitTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateUpload_AtTheCap_ReturnsLimitExceeded_AndStoresNothing_CA_058_07()
    {
        using var harness = new Harness();
        for (var i = 0; i < 10; i++)
        {
            (await harness.CreateAsync("owner-a")).Status.Should().Be(MediaOutcomeStatus.Success);
        }

        var eleventh = await harness.CreateAsync("owner-a");

        eleventh.Status.Should().Be(MediaOutcomeStatus.LimitExceeded);
        eleventh.RetryAfterSeconds.Should().Be(1800, "the oldest pending file was created now and stops counting in 30 minutes");
        harness.Files.Count.Should().Be(10);
    }

    [Fact]
    public async Task CreateUpload_RetryAfter_ShrinksAsTheOldestPendingFileAges_R_11()
    {
        using var harness = new Harness();
        for (var i = 0; i < 10; i++)
        {
            await harness.CreateAsync("owner-a");
        }

        harness.Clock.Advance(TimeSpan.FromMinutes(10));
        var blocked = await harness.CreateAsync("owner-a");

        blocked.RetryAfterSeconds.Should().Be(1200);
    }

    [Fact]
    public async Task CreateUpload_ConfirmedRejectedAndExpiredDoNotCount_AndOtherOwnersAreUnaffected_CA_058_10()
    {
        using var harness = new Harness();
        var ids = new List<Guid>();
        for (var i = 0; i < 10; i++)
        {
            ids.Add((await harness.CreateAsync("owner-a")).Value!.Id);
        }

        (await harness.CreateAsync("owner-a")).Status.Should().Be(MediaOutcomeStatus.LimitExceeded);
        var ready = harness.Files.Get(ids[0]).MarkReady(10, 10, _start);
        var rejected = harness.Files.Get(ids[1]).MarkRejected(_start);
        (await harness.Files.TryReplaceAsync(ready, "1", CancellationToken.None)).Should().BeTrue();
        (await harness.Files.TryReplaceAsync(rejected, "1", CancellationToken.None)).Should().BeTrue();

        (await harness.CreateAsync("owner-a")).Status.Should().Be(MediaOutcomeStatus.Success, "a ready and a rejected file free two slots");
        (await harness.CreateAsync("owner-b")).Status.Should().Be(MediaOutcomeStatus.Success, "another person has a separate cap");
        harness.Clock.Advance(TimeSpan.FromMinutes(31));
        (await harness.CreateAsync("owner-a")).Status.Should().Be(MediaOutcomeStatus.Success, "a pending file past its window no longer counts");
    }

    [Fact]
    public async Task CreateUpload_ConcurrentRequests_NeverLeaveMoreThanTheCap_CA_058_08_CA_058_09()
    {
        using var harness = new Harness();
        for (var i = 0; i < 8; i++)
        {
            await harness.CreateAsync("owner-a");
        }

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 25).Select(_ => Task.Run(() => harness.CreateAsync("owner-a"))));

        outcomes.Count(outcome => outcome.Status == MediaOutcomeStatus.Success).Should().Be(2);
        outcomes.Count(outcome => outcome.Status == MediaOutcomeStatus.LimitExceeded).Should().Be(23);
        harness.Files.Count.Should().Be(10);
    }

    [Fact]
    public async Task CreateUpload_Counters_HoldOnlyCounts_CA_058_19()
    {
        using var harness = new Harness();
        var measurements = new List<(string Name, long Value, int TagCount)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            // Solo los instrumentos de este arnés: cada MediaMetrics crea un medidor con el mismo nombre, y escuchar por nombre mezclaba las
            // mediciones de pruebas en paralelo, que además escribían a la vez en la lista y podían perder una.
            if (instrument == harness.Metrics.FilesCreated || instrument == harness.Metrics.UploadsRejectedByLimit)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => measurements.Add((instrument.Name, value, tags.Length)));
        listener.Start();
        for (var i = 0; i < 11; i++)
        {
            await harness.CreateAsync("synthetic-sub");
        }

        measurements.Where(m => m.Name == "knock.media.files.created").Sum(m => m.Value).Should().Be(10);
        measurements.Where(m => m.Name == "knock.media.uploads.rejected_by_limit").Sum(m => m.Value).Should().Be(1);
        measurements.Should().OnlyContain(m => m.TagCount == 0, "the counters carry no tags, so no sub or blob name can leak");
    }

    [Fact]
    public void Options_RejectAnInvalidCap_R_09()
    {
        var invalid = new MediaOptions { MaxPendingUploads = 0 };

        Validator.TryValidateObject(invalid, new ValidationContext(invalid), [], validateAllProperties: true).Should().BeFalse();
        new MediaOptions().MaxPendingUploads.Should().Be(10);
    }

    private sealed class Harness : IDisposable
    {
        private readonly ImageProcessor _processor = new(Options.Create(new MediaOptions { ReducedMaxSide = 512, MaxConcurrentProcessing = 1 }));
        private readonly MediaMetrics _metrics = new();

        public Harness()
        {
            Service = new MediaService(Files, new InMemoryMediaBlobStore(), _processor, new AllowAll(), Clock, Options.Create(new MediaOptions()), _metrics);
        }

        public Counting Files { get; } = new();

        public MediaMetrics Metrics => _metrics;

        public ManualClock Clock { get; } = new(_start);

        public MediaService Service { get; }

        public Task<MediaOutcome<UploadTicket>> CreateAsync(string owner) =>
            Service.CreateUploadAsync(owner, "solicitud", "image/jpeg", 1000, CancellationToken.None);

        public void Dispose()
        {
            _processor.Dispose();
            _metrics.Dispose();
        }
    }

    /// <summary>In-memory store that also lets the test read what was stored.</summary>
    private sealed class Counting : IMediaFileStore
    {
        private readonly InMemoryMediaFileStore _inner = new();
        private readonly List<Guid> _ids = [];
        private readonly Lock _gate = new();

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _ids.Count;
                }
            }
        }

        public MediaFile Get(Guid id) => _inner.FindAsync(id, CancellationToken.None).Result!.File;

        public Task AddAsync(MediaFile file, CancellationToken cancellationToken) => _inner.AddAsync(file, cancellationToken);

        public async Task<PendingAddResult> AddPendingWithinLimitAsync(MediaFile file, int limit, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var result = await _inner.AddPendingWithinLimitAsync(file, limit, now, cancellationToken);
            if (result.Added)
            {
                lock (_gate)
                {
                    _ids.Add(file.Id);
                }
            }

            return result;
        }

        public Task<StoredMediaFile?> FindAsync(Guid id, CancellationToken cancellationToken) => _inner.FindAsync(id, cancellationToken);

        public Task<bool> TryReplaceAsync(MediaFile file, string expectedVersion, CancellationToken cancellationToken) => _inner.TryReplaceAsync(file, expectedVersion, cancellationToken);
    }

    private sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class AllowAll : IContentModerator
    {
        public Task<ModerationResult> ReviewTextAsync(string text, CancellationToken cancellationToken) => Task.FromResult(new ModerationResult(ModerationOutcome.Allowed));

        public Task<ModerationResult> ReviewImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) => Task.FromResult(new ModerationResult(ModerationOutcome.Allowed));
    }
}
