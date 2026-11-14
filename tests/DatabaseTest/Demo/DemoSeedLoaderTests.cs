extern alias identity;
using System.Security.Cryptography;
using System.Text;
using Knock.IntegrationTest.Support;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Xunit;
using DemoClientSeeder = identity::Knock.Identity.Api.Modules.Usuarios.Services.DemoClientSeeder;
using IProfileCache = identity::Knock.Identity.Api.Modules.Usuarios.Data.IProfileCache;
using KnockMongoOptions = identity::Knock.Identity.Api.Hosting.KnockMongoOptions;
using PhoneProtectionKeys = identity::Knock.Identity.Api.Modules.Sesiones.Services.PhoneProtectionKeys;
using PhoneProtectionOptions = identity::Knock.Identity.Api.Modules.Sesiones.Services.PhoneProtectionOptions;
using PhoneProtector = identity::Knock.Identity.Api.Modules.Sesiones.Services.PhoneProtector;
using UserIdDerivation = identity::Knock.Identity.Api.Modules.Sesiones.Services.UserIdDerivation;
using UserProfile = identity::Knock.Identity.Api.Modules.Usuarios.Domain.UserProfile;
using UsuariosStore = identity::Knock.Identity.Api.Modules.Usuarios.Data.UsuariosStore;

namespace Knock.DatabaseTest.Demo;

public sealed class DemoSeedLoaderTests
{
    private static readonly string _path = Path.Combine(CatalogTestDatabase.SeedsDirectory, "demo", "identity", "usuarios.json");

    [Theory]
    [InlineData("sub")]
    [InlineData("celularHash")]
    public async Task UniqueConflict_CA_038_11(string field)
    {
        await WithDatabase(async (database, seeder, _) =>
        {
            var users = database.GetCollection<BsonDocument>("usuarios");
            await seeder.SeedAsync(_path, CancellationToken.None);
            var sample = await users.Find(FilterDefinition<BsonDocument>.Empty).FirstAsync();
            await users.DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
            var existing = new BsonDocument { ["_id"] = ObjectId.GenerateNewId(), [field] = sample[field], ["demo"] = false };
            await users.InsertOneAsync(existing);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync(_path, CancellationToken.None));
            Assert.Contains(field, error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(sample["celularHash"].ToString()!, error.ToString(), StringComparison.Ordinal);
            var remaining = await users.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
            Assert.Equal(existing, Assert.Single(remaining));
        });
    }

    [Fact]
    public async Task InvalidLastEntryWritesNothing()
    {
        await WithDatabase(async (database, seeder, _) =>
        {
            var seeds = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(_path))!.AsArray();
            seeds[9]!["celular"] = "invalid";
            var temporary = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(temporary, seeds.ToJsonString());
                await Assert.ThrowsAnyAsync<Exception>(() => seeder.SeedAsync(temporary, CancellationToken.None));
                Assert.Equal(0, await database.GetCollection<BsonDocument>("usuarios").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            }
            finally { File.Delete(temporary); }
        });
    }

    [Fact]
    public async Task DemoAccountState_CA_038_13()
    {
        await WithDatabase(async (database, seeder, _) =>
        {
            await seeder.SeedAsync(_path, CancellationToken.None);
            var users = await database.GetCollection<BsonDocument>("usuarios").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
            var root = Directory.GetParent(Directory.GetParent(CatalogTestDatabase.SeedsDirectory)!.FullName)!.FullName;
            using var config = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "src", "Knock.Identity.Api", "appsettings.json")));
            var versions = config.RootElement.GetProperty("Consentimientos");
            Assert.Equal(10, users.Count);
            foreach (var user in users)
            {
                Assert.Equal("activa", user["estado"].AsString);
                Assert.True(user["perfilCompleto"].AsBoolean);
                var consents = user["consentimientos"].AsBsonArray.Select(x => x.AsBsonDocument).ToArray();
                Assert.Equal(2, consents.Length);
                Assert.Equal(versions.GetProperty("AvisoPrivacidad").GetString(), Assert.Single(consents, c => c["documento"] == "aviso_privacidad")["version"].AsString);
                Assert.Equal(versions.GetProperty("Terminos").GetString(), Assert.Single(consents, c => c["documento"] == "terminos")["version"].AsString);
            }
        });
    }

    [Fact]
    public async Task EveryLoadInvalidatesEveryProfile()
    {
        await WithDatabase(async (database, seeder, _, cache) =>
        {
            var users = database.GetCollection<BsonDocument>("usuarios");
            await seeder.SeedAsync(_path, CancellationToken.None);
            await seeder.SeedAsync(_path, CancellationToken.None);
            Assert.Equal(20, cache.Removed.Count);
            var user = await users.Find(FilterDefinition<BsonDocument>.Empty).FirstAsync();
            await users.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", user["_id"]), Builders<BsonDocument>.Update.Set("nombre", "Demo Modificado"));
            await seeder.SeedAsync(_path, CancellationToken.None);
            Assert.Equal(30, cache.Removed.Count);
            await seeder.SeedAsync(_path, CancellationToken.None);
            Assert.Equal(40, cache.Removed.Count);
        });
    }

    [Fact]
    public async Task ConcurrentLoads_CA_038_11()
    {
        await WithDatabase(async (database, seeder, _) =>
        {
            var users = database.GetCollection<BsonDocument>("usuarios");
            await users.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("sub"), new CreateIndexOptions { Unique = true }));
            await users.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("celularHash"), new CreateIndexOptions { Unique = true }));
            await Task.WhenAll(seeder.SeedAsync(_path, CancellationToken.None), seeder.SeedAsync(_path, CancellationToken.None));
            Assert.Equal(10, await users.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("demo", true)));
        });
    }

    [Fact]
    public async Task CacheFailureDoesNotBlockLoadAndNextLoadRetries()
    {
        await WithDatabase(async (database, seeder, _, cache) =>
        {
            cache.Fail = true;
            await seeder.SeedAsync(_path, CancellationToken.None);
            Assert.Equal(10, await database.GetCollection<BsonDocument>("usuarios").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Empty(cache.Removed);
            cache.Fail = false;
            await seeder.SeedAsync(_path, CancellationToken.None);
            Assert.Equal(10, cache.Removed.Count);
        });
    }

    [Fact]
    public async Task PartialWriteCanBeRetried_CA_038_11()
    {
        await WithDatabase(async (database, seeder, _) =>
        {
            var users = database.GetCollection<BsonDocument>("usuarios");
            await users.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("nombre"), new CreateIndexOptions { Unique = true }));
            var seeds = BsonSerializer.Deserialize<BsonArray>(await File.ReadAllTextAsync(_path));
            seeds[5].AsBsonDocument["nombre"] = seeds[0]["nombre"];
            var temporary = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(temporary, seeds.ToJson());
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync(temporary, CancellationToken.None));
                Assert.Contains("unique index", error.Message, StringComparison.Ordinal);
                Assert.Equal(5, await users.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
                await seeder.SeedAsync(_path, CancellationToken.None);
                Assert.Equal(10, await users.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            }
            finally { File.Delete(temporary); }
        });
    }

    [Fact]
    public async Task DerivedSub_CA_038_04()
    {
        await WithDatabase(async (database, seeder, _) =>
        {
            await seeder.SeedAsync(_path, CancellationToken.None);
            var users = await database.GetCollection<BsonDocument>("usuarios").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
            var seeds = BsonSerializer.Deserialize<BsonArray>(await File.ReadAllTextAsync(_path));
            Assert.Equal(10, users.Count);
            foreach (var seed in seeds.Select(item => item.AsBsonDocument))
            {
                var user = Assert.Single(users, item => item["_id"] == seed["_id"]);
                Assert.Equal(UserIdDerivation.Derive(seed["uid"].AsString).ToString("D"), user["sub"].AsString);
                Assert.False(user.Contains("uid"));
            }
        });
    }

    [Fact]
    public async Task ChangedContentIncrementsVersion_CA_038_05()
    {
        await WithDatabase(async (database, seeder, _) =>
        {
            var collection = database.GetCollection<BsonDocument>("usuarios");
            await seeder.SeedAsync(_path, CancellationToken.None);
            var before = await collection.Find(FilterDefinition<BsonDocument>.Empty).FirstAsync();
            await collection.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", before["_id"]),
                Builders<BsonDocument>.Update.Set("nombre", "Demo Modificado"));
            await seeder.SeedAsync(_path, CancellationToken.None);
            var after = await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", before["_id"])).FirstAsync();
            Assert.Equal(before["version"].AsInt32 + 1, after["version"].AsInt32);
            Assert.Equal(before["nombre"], after["nombre"]);
        });
    }

    [Fact]
    public async Task RepeatedLoad_CA_038_05()
    {
        await WithDatabase(async (database, seeder, key) =>
        {
            var collection = database.GetCollection<BsonDocument>("usuarios");
            await seeder.SeedAsync(_path, CancellationToken.None);
            var first = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
            await seeder.SeedAsync(_path, CancellationToken.None);
            var second = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
            var seeds = BsonSerializer.Deserialize<BsonArray>(await File.ReadAllTextAsync(_path));
            Assert.Equal(10, second.Count);
            foreach (var seed in seeds.Select(item => item.AsBsonDocument))
            {
                var before = Assert.Single(first, item => item["_id"] == seed["_id"]);
                var user = Assert.Single(second, item => item["_id"] == seed["_id"]);
                Assert.True(user["demo"].AsBoolean);
                Assert.Equal(before["version"], user["version"]);
                Assert.Equal(before["celularHash"], user["celularHash"]);
                Assert.Equal(1, user["celularCifradoVersion"].AsInt32);
                Assert.Equal(1, user["celularHashVersion"].AsInt32);
                Assert.False(user.Contains("celular"));
                var cipher = user["celularCifrado"].AsBsonBinaryData.Bytes;
                var plaintext = new byte[cipher.Length - 28];
                using var aes = new AesGcm(key, 16);
                aes.Decrypt(cipher.AsSpan(0, 12), cipher.AsSpan(12, plaintext.Length), cipher.AsSpan(12 + plaintext.Length), plaintext,
                    Encoding.UTF8.GetBytes("usuarios|celular|" + seed["_id"].AsObjectId));
                Assert.Equal(seed["celular"].AsString, Encoding.UTF8.GetString(plaintext));
            }
        });
    }

    private static async Task WithDatabase(Func<IMongoDatabase, DemoClientSeeder, byte[], Task> test)
        => await WithDatabase((db, seeder, key, _) => test(db, seeder, key));

    private static async Task WithDatabase(Func<IMongoDatabase, DemoClientSeeder, byte[], FakeCache, Task> test)
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        using var client = new MongoClient(fixture.ConnectionString);
        var name = "s038_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
        try
        {
            var database = client.GetDatabase(name);
            var key = RandomNumberGenerator.GetBytes(32);
            var options = new PhoneProtectionOptions { EncryptionKeyVersion = 1, HmacKeyVersion = 1 };
            options.EncryptionKeys.Add(1, Convert.ToBase64String(key));
            options.HmacKeys.Add(1, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            var protector = new PhoneProtector(PhoneProtectionKeys.TryCreate(options, out var errors) ?? throw new InvalidOperationException(string.Join(',', errors)));
            var store = new UsuariosStore(database, Options.Create(new KnockMongoOptions()));
            var cache = new FakeCache();
            await test(database, new DemoClientSeeder(store, protector, cache, Microsoft.Extensions.Logging.Abstractions.NullLogger<DemoClientSeeder>.Instance), key, cache);
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }

    private sealed class FakeCache : IProfileCache
    {
        public List<string> Removed { get; } = [];
        public bool Fail { get; set; }
        public Task<UserProfile?> GetAsync(string sub, CancellationToken cancellationToken) => Task.FromResult<UserProfile?>(null);
        public Task SetAsync(UserProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RemoveAsync(string sub, CancellationToken cancellationToken)
        {
            if (Fail) { throw new TimeoutException(); }
            lock (Removed) { Removed.Add(sub); }
            return Task.CompletedTask;
        }
    }
}
