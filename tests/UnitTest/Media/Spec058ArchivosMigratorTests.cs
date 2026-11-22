using AwesomeAssertions;
using Knock.Market.Api.Modules.Media.Data;
using Xunit;

namespace Knock.UnitTest.Media;

/// <summary>Spec 058 (T-03, R-08, R-12, R-16, R-17): el migrador traduce, es idempotente, descarta pendientes vencidos y cuenta fallidos sin datos.</summary>
public sealed class Spec058ArchivosMigratorTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    private static BinaryData Legacy(Guid id, string estado, DateTimeOffset createdAt) => BinaryData.FromString(
        $$"""
        {"id":"{{id}}","blob_key":"{{Guid.NewGuid():N}}","propietario_id":"synthetic-sub","uso":"solicitud","estado":"{{estado}}","content_type":"image/jpeg","tamano_bytes":1000,"creado_en":"{{createdAt:O}}"}
        """);

    private static async IAsyncEnumerable<BinaryData> Source(params BinaryData[] items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    [Fact]
    public async Task Migrate_TranslatesDiscardsExpiredAndCountsFailures_CA_058_12()
    {
        var store = new InMemoryMediaFileStore();
        var migrator = new ArchivosMigrator(store);
        var ready = Guid.CreateVersion7();
        var live = Guid.CreateVersion7();
        var expired = Guid.CreateVersion7();
        var discarded = new List<string>();

        var report = await migrator.MigrateAsync(
            Source(
                Legacy(ready, "listo", _now.AddDays(-2)),
                Legacy(live, "pendiente", _now.AddMinutes(-5)),
                Legacy(expired, "pendiente", _now.AddHours(-3)),
                BinaryData.FromString("{not json")),
            (file, _) =>
            {
                discarded.Add(file.BlobKey);
                return Task.CompletedTask;
            },
            _now,
            CancellationToken.None);

        report.Should().Be(new ArchivosMigrationReport(4, 2, 0, 1, 1));
        discarded.Should().ContainSingle();
        (await store.FindAsync(ready, CancellationToken.None)).Should().NotBeNull();
        (await store.FindAsync(live, CancellationToken.None)).Should().NotBeNull();
        (await store.FindAsync(expired, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Migrate_RunTwice_IsIdempotent_CA_058_13()
    {
        var store = new InMemoryMediaFileStore();
        var migrator = new ArchivosMigrator(store);
        var record = Legacy(Guid.CreateVersion7(), "listo", _now.AddDays(-1));

        await migrator.MigrateAsync(Source(record), (_, _) => Task.CompletedTask, _now, CancellationToken.None);
        var second = await migrator.MigrateAsync(Source(record), (_, _) => Task.CompletedTask, _now, CancellationToken.None);

        second.Should().Be(new ArchivosMigrationReport(1, 0, 1, 0, 0));
    }
}
