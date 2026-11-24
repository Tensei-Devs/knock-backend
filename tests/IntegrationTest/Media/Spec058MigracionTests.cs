extern alias MarketApi;

using System.Text.Json;
using AwesomeAssertions;
using Azure.Storage.Blobs;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Hosting;
using MarketApi::Knock.Market.Api.Modules.Media.Data;
using MarketApi::Knock.Market.Api.Modules.Media.Domain;
using MarketApi::Knock.Market.Api.Modules.Media.Services;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.IntegrationTest.Media;

/// <summary>
/// Spec 058, T-03 (R-08, R-16, R-17; CA-12 a CA-15): el migrador contra Azurite (contenedor antiguo de registros y cuarentena) y MongoDB reales, con el índice único de <c>archivoId</c> que crea el script de <c>db/</c>.
/// Solo datos ficticios; las corre el CI.
/// </summary>
public sealed class Spec058MigracionTests
{
    private const string _legacyContainer = "registros-antiguos-prueba";

    private static MongoClient? _client;

    [Fact]
    public async Task Migra_Listo_Rechazado_Y_Pendiente_Vigente_Y_Descarta_El_Vencido_CA_058_12()
    {
        await using var azurite = await AzuriteFixture.StartAsync();
        var (runner, store, options, _) = await CreateAsync(azurite);
        var now = DateTimeOffset.UtcNow;
        var ready = await PutLegacyAsync(azurite, "listo", now.AddDays(-2));
        var rejected = await PutLegacyAsync(azurite, "rechazado", now.AddDays(-1));
        var live = await PutLegacyAsync(azurite, "pendiente", now.AddMinutes(-5));
        var expired = await PutLegacyAsync(azurite, "pendiente", now.AddHours(-3));
        await PutQuarantineAsync(azurite, options, expired.BlobKey);

        var report = await runner.RunAsync(new BlobServiceClient(azurite.ConnectionString), _legacyContainer, CancellationToken.None);

        report.Should().Be(new ArchivosMigrationReport(4, 3, 0, 1, 0));
        ArchivosMigrationRunner.SourceCanBeRemoved(report).Should().BeTrue();
        foreach (var legacy in new[] { ready, rejected, live })
        {
            var migrated = (await store.FindAsync(legacy.Id, CancellationToken.None))!.File;
            migrated.BlobKey.Should().Be(legacy.BlobKey);
            migrated.OwnerId.Should().Be(legacy.OwnerId);
        }

        (await store.FindAsync(expired.Id, CancellationToken.None)).Should().BeNull("el pendiente vencido no se migra");
        (await azurite.ListBlobNamesAsync(options.QuarantineContainer)).Should().BeEmpty("se borra el blob de cuarentena del pendiente vencido");
        (await azurite.ListBlobNamesAsync(_legacyContainer)).Should().HaveCount(4, "el contenedor de origen no se toca");
    }

    [Fact]
    public async Task Migracion_Es_Idempotente_Y_Reanudable_Sin_Sobrescribir_CA_058_13()
    {
        await using var azurite = await AzuriteFixture.StartAsync();
        var (runner, store, _, files) = await CreateAsync(azurite);
        var service = new BlobServiceClient(azurite.ConnectionString);
        var now = DateTimeOffset.UtcNow;
        var first = await PutLegacyAsync(azurite, "pendiente", now.AddMinutes(-5));
        var second = await PutLegacyAsync(azurite, "listo", now.AddDays(-1));

        (await runner.RunAsync(service, _legacyContainer, CancellationToken.None)).Should().Be(new ArchivosMigrationReport(2, 2, 0, 0, 0));
        var advanced = (await store.FindAsync(first.Id, CancellationToken.None))!;
        (await store.TryReplaceAsync(advanced.File.MarkReady(10, 10, now), advanced.Version, CancellationToken.None)).Should().BeTrue();
        var third = await PutLegacyAsync(azurite, "rechazado", now.AddHours(-1));

        var resumed = await runner.RunAsync(service, _legacyContainer, CancellationToken.None);
        var again = await runner.RunAsync(service, _legacyContainer, CancellationToken.None);

        resumed.Should().Be(new ArchivosMigrationReport(3, 1, 2, 0, 0), "reanuda: lo nuevo se inserta y lo ya migrado se cuenta como presente");
        again.Should().Be(new ArchivosMigrationReport(3, 0, 3, 0, 0));
        (await files.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(3, "sin duplicados");
        (await store.FindAsync(first.Id, CancellationToken.None))!.File.State.Should().Be(MediaFileState.Ready, "un documento que ya avanzó nunca se sobrescribe");
        (await store.FindAsync(second.Id, CancellationToken.None)).Should().NotBeNull();
        (await store.FindAsync(third.Id, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task Contenedor_Inexistente_O_Vacio_Migra_Cero_Y_No_Lo_Crea_CA_058_14()
    {
        await using var azurite = await AzuriteFixture.StartAsync();
        var (runner, _, _, files) = await CreateAsync(azurite);
        var service = new BlobServiceClient(azurite.ConnectionString);

        var missing = await runner.RunAsync(service, _legacyContainer, CancellationToken.None);
        var exists = await azurite.Container(_legacyContainer).ExistsAsync();
        await azurite.Container(_legacyContainer).CreateIfNotExistsAsync();
        var empty = await runner.RunAsync(service, _legacyContainer, CancellationToken.None);

        missing.Should().Be(new ArchivosMigrationReport(0, 0, 0, 0, 0));
        exists.Value.Should().BeFalse("el migrador no crea el contenedor de origen");
        empty.Should().Be(new ArchivosMigrationReport(0, 0, 0, 0, 0));
        ArchivosMigrationRunner.SourceCanBeRemoved(empty).Should().BeTrue();
        (await files.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(0);
    }

    [Fact]
    public async Task Registro_Ilegible_O_Con_Estado_Desconocido_Cuenta_Como_Fallido_Y_No_Se_Puede_Borrar_CA_058_15()
    {
        await using var azurite = await AzuriteFixture.StartAsync();
        var (runner, store, _, _) = await CreateAsync(azurite);
        var good = await PutLegacyAsync(azurite, "listo", DateTimeOffset.UtcNow.AddDays(-1));
        var unknownState = await PutLegacyAsync(azurite, "borrado", DateTimeOffset.UtcNow.AddDays(-1));
        await azurite.Container(_legacyContainer).UploadBlobAsync("ilegible.json", BinaryData.FromString("{no es json"));

        var report = await runner.RunAsync(new BlobServiceClient(azurite.ConnectionString), _legacyContainer, CancellationToken.None);

        report.Should().Be(new ArchivosMigrationReport(3, 1, 0, 0, 2), "migra el resto y cuenta los dos fallidos");
        ArchivosMigrationRunner.SourceCanBeRemoved(report).Should().BeFalse("con fallidos el contenedor no queda listo para borrarse");
        (await store.FindAsync(good.Id, CancellationToken.None)).Should().NotBeNull();
        (await store.FindAsync(unknownState.Id, CancellationToken.None)).Should().BeNull();
    }

    private static async Task<(ArchivosMigrationRunner Runner, MongoMediaFileStore Store, MediaOptions Options, IMongoCollection<BsonDocument> Files)> CreateAsync(AzuriteFixture azurite)
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        _client ??= new MongoClient(documentDb.ConnectionString);
        var database = _client.GetDatabase("s058_mig_" + Guid.NewGuid().ToString("N"));
        var files = database.GetCollection<BsonDocument>(MongoMediaFileStore.CollectionName);
        await files.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("archivoId"), new CreateIndexOptions { Unique = true, Name = "archivoId_1" }));
        var options = new MediaOptions();
        var store = new MongoMediaFileStore(database, Options.Create(new KnockMongoOptions()));
        var blobs = new AzureMediaBlobStore(new BlobServiceClient(azurite.ConnectionString), new EnlaceSubidaNoDisponible(), Options.Create(options));
        return (new ArchivosMigrationRunner(store, blobs, TimeProvider.System), store, options, files);
    }

    private static async Task<Legacy> PutLegacyAsync(AzuriteFixture azurite, string state, DateTimeOffset createdAt)
    {
        var legacy = new Legacy(Guid.CreateVersion7(), Guid.NewGuid().ToString("N"), "sub-" + Guid.NewGuid().ToString("N")[..8]);
        var json = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["id"] = legacy.Id,
            ["blob_key"] = legacy.BlobKey,
            ["propietario_id"] = legacy.OwnerId,
            ["uso"] = "solicitud",
            ["estado"] = state,
            ["content_type"] = "image/jpeg",
            ["tamano_bytes"] = 1000,
            ["creado_en"] = createdAt,
        });
        var container = azurite.Container(_legacyContainer);
        await container.CreateIfNotExistsAsync();
        await container.UploadBlobAsync($"{legacy.Id:N}.json", BinaryData.FromString(json));
        return legacy;
    }

    private static async Task PutQuarantineAsync(AzuriteFixture azurite, MediaOptions options, string blobKey)
    {
        var container = azurite.Container(options.QuarantineContainer);
        await container.CreateIfNotExistsAsync();
        await container.UploadBlobAsync(blobKey, BinaryData.FromString("synthetic"));
    }

    private sealed record Legacy(Guid Id, string BlobKey, string OwnerId);
}
