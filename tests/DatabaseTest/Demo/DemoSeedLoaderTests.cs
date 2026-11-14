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
using KnockMongoOptions = identity::Knock.Identity.Api.Hosting.KnockMongoOptions;
using PhoneProtectionKeys = identity::Knock.Identity.Api.Modules.Sesiones.Services.PhoneProtectionKeys;
using PhoneProtectionOptions = identity::Knock.Identity.Api.Modules.Sesiones.Services.PhoneProtectionOptions;
using PhoneProtector = identity::Knock.Identity.Api.Modules.Sesiones.Services.PhoneProtector;
using UserIdDerivation = identity::Knock.Identity.Api.Modules.Sesiones.Services.UserIdDerivation;
using UsuariosStore = identity::Knock.Identity.Api.Modules.Usuarios.Data.UsuariosStore;

namespace Knock.DatabaseTest.Demo;

public sealed class DemoSeedLoaderTests
{
    private static readonly string _path = Path.Combine(CatalogTestDatabase.SeedsDirectory, "demo", "identity", "usuarios.json");

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
            await test(database, new DemoClientSeeder(store, protector), key);
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }
}
