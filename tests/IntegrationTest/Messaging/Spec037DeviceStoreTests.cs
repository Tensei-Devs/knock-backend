extern alias MessagingApi;
using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using DeviceRegistrationStatus = MessagingApi::Knock.Messaging.Api.Modules.Avisos.Domain.DeviceRegistrationStatus;
using DeviceStore = MessagingApi::Knock.Messaging.Api.Modules.Avisos.Data.DeviceStore;
using TokenProtection = MessagingApi::Knock.Messaging.Api.Modules.Avisos.Services.TokenProtection;

namespace Knock.IntegrationTest.Messaging;

public sealed class Spec037DeviceStoreTests
{
    [Fact]
    public async Task Registration_WithoutUniqueFingerprintIndex_CanCreateDuplicates()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        using var client = new MongoClient(fixture.ConnectionString);
        var database = client.GetDatabase("s037_noindex_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8)));
        try
        {
            var protection = new TokenProtection("1", RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32));
            var store = new DeviceStore(database, protection);
            var first = await store.RegisterAsync("owner", "sid", "ios", "synthetic-token", DateTime.UtcNow, CancellationToken.None);
            var second = await store.RegisterAsync("owner", "sid", "ios", "synthetic-token", DateTime.UtcNow, CancellationToken.None);
            first.Status.Should().Be(DeviceRegistrationStatus.Created);
            second.Status.Should().Be(DeviceRegistrationStatus.Created);
            var count = await database.GetCollection<BsonDocument>("notifications.dispositivos").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
            count.Should().Be(2);
        }
        finally
        {
            await client.DropDatabaseAsync(database.DatabaseNamespace.DatabaseName);
        }
    }

    [Fact]
    public async Task Registration_IsAtomicAndDeletionIsOwnerScoped()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        using var client = new MongoClient(fixture.ConnectionString);
        var database = client.GetDatabase("s037_store_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8)));
        var devices = database.GetCollection<BsonDocument>("notifications.dispositivos");
        await devices.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("tokenHuella"), new CreateIndexOptions { Unique = true, Name = "tokenHuella_1" }));
        await devices.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("id"), new CreateIndexOptions { Unique = true, Name = "id_1" }));
        try
        {
            var protection = new TokenProtection("1", RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32));
            var store = new DeviceStore(database, protection);
            var now = DateTime.UtcNow;
            var registrations = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
                store.RegisterAsync("owner", "session", "android", "synthetic-token", now, CancellationToken.None)));
            registrations.Count(result => result.Status == DeviceRegistrationStatus.Created).Should().Be(1);
            registrations.Count(result => result.Status == DeviceRegistrationStatus.Existing).Should().Be(4);
            registrations.Select(result => result.Id).Distinct().Should().ContainSingle();
            var id = registrations[0].Id!;
            var repeated = await store.RegisterAsync("owner", "new-session", "ios", "synthetic-token", now.AddMinutes(1), CancellationToken.None);
            repeated.Status.Should().Be(DeviceRegistrationStatus.Existing);
            repeated.Id.Should().Be(id);
            var before = await devices.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
            before["_id"].IsObjectId.Should().BeTrue();
            before["id"].AsString.Should().Be(id);
            before["sid"].AsString.Should().Be("new-session");
            before["plataforma"].AsString.Should().Be("ios");
            before["renovadoEn"].ToUniversalTime().Should().BeCloseTo(now.AddMinutes(1), TimeSpan.FromMilliseconds(2));
            before.ToJson().Should().NotContain("synthetic-token");
            protection.Decrypt(before["identificadorCifrado"].AsString, "owner", id).Should().Be("synthetic-token");
            var other = await store.RegisterAsync("other", "other-session", "ios", "synthetic-token", now.AddMinutes(1), CancellationToken.None);
            other.Status.Should().Be(DeviceRegistrationStatus.Conflict);
            (await devices.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync()).ToJson().Should().Be(before.ToJson());
            (await store.DeleteAsync("other", id, CancellationToken.None)).Should().BeFalse();
            (await store.DeleteAsync("owner", id, CancellationToken.None)).Should().BeTrue();
            (await store.DeleteAsync("owner", id, CancellationToken.None)).Should().BeFalse();
        }
        finally
        {
            await client.DropDatabaseAsync(database.DatabaseNamespace.DatabaseName);
        }
    }
}
