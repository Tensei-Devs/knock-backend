using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Media.Data;
using Knock.Market.Api.Modules.Media.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.DatabaseTest;

/// <summary>Spec 058 (T-02 y T-04): <c>MongoMediaFileStore</c> contra una base real con los índices del script <c>db/049</c> (corre solo en CI con Docker).</summary>
public sealed class Spec058MongoMediaFileStoreTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Add_DuplicateArchivoId_Throws_AndKeepsTheOriginal_CA_058_02()
    {
        await using var harness = await Harness.StartAsync();
        var file = MediaFile.CreatePending("owner-a", MediaUsage.Request, "image/jpeg", 100, _now);
        await harness.Store.AddAsync(file, CancellationToken.None);

        var again = () => harness.Store.AddAsync(file with { OwnerId = "owner-b" }, CancellationToken.None);

        await again.Should().ThrowAsync<InvalidOperationException>();
        (await harness.Store.FindAsync(file.Id, CancellationToken.None))!.File.OwnerId.Should().Be("owner-a");
    }

    [Fact]
    public async Task Add_Find_StoresCamelCaseDocumentAndRoundTrips_CA_058_03()
    {
        await using var harness = await Harness.StartAsync();
        var file = MediaFile.CreatePending("owner-a", MediaUsage.Profile, "image/png", 2048, _now);

        await harness.Store.AddAsync(file, CancellationToken.None);

        var raw = await harness.Collection.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        raw["_id"].BsonType.Should().Be(BsonType.ObjectId);
        raw.Names.Where(name => name != "_id").Should().OnlyContain(name => !name.Contains('_'));
        var found = await harness.Store.FindAsync(file.Id, CancellationToken.None);
        found!.File.Should().Be(file);
        found.Version.Should().Be("1");
    }

    [Fact]
    public async Task TryReplace_TwoConcurrentConfirmations_OnlyOneWins_AndVersionRisesByOne_CA_058_05()
    {
        await using var harness = await Harness.StartAsync();
        var pending = MediaFile.CreatePending("owner-a", MediaUsage.Request, "image/jpeg", 100, _now);
        await harness.Store.AddAsync(pending, CancellationToken.None);
        var ready = pending.MarkReady(10, 10, _now.AddMinutes(1));

        var results = await Task.WhenAll(
            harness.Store.TryReplaceAsync(ready, "1", CancellationToken.None),
            harness.Store.TryReplaceAsync(ready, "1", CancellationToken.None));

        results.Count(won => won).Should().Be(1);
        var stored = await harness.Store.FindAsync(pending.Id, CancellationToken.None);
        stored!.Version.Should().Be("2");
        stored.File.State.Should().Be(MediaFileState.Ready);
        stored.File.ExpiresAt.Should().Be(MediaRules.NeverExpires);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly MongoClient _client;
        private readonly string _name;

        private Harness(MongoClient client, string name, MongoMediaFileStore store)
        {
            _client = client;
            _name = name;
            Store = store;
        }

        public MongoMediaFileStore Store { get; }

        public IMongoCollection<BsonDocument> Collection => _client.GetDatabase(_name).GetCollection<BsonDocument>(MongoMediaFileStore.CollectionName);

        public static async Task<Harness> StartAsync()
        {
            var fixture = await DocumentDbFixture.SharedAsync();
            var client = new MongoClient(fixture.ConnectionString);
            var name = "s058_" + Guid.NewGuid().ToString("N");
            var database = client.GetDatabase(name);
            await database.GetCollection<BsonDocument>(MongoMediaFileStore.CollectionName).Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("archivoId"), new CreateIndexOptions { Unique = true, Name = "archivoId_1" }));
            return new Harness(client, name, new MongoMediaFileStore(database, Options.Create(new KnockMongoOptions())));
        }

        public async ValueTask DisposeAsync()
        {
            await _client.DropDatabaseAsync(_name);
            _client.Dispose();
        }
    }
}
