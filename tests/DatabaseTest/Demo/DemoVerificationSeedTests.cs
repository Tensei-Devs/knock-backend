extern alias identity;
using System.Security.Cryptography;
using System.Text.Json;
using Knock.IntegrationTest.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using DemoVerificationSeeder = identity::Knock.Identity.Api.Modules.Trust.Services.DemoVerificationSeeder;
using DemoVerificationStore = identity::Knock.Identity.Api.Modules.Trust.Data.DemoVerificationStore;
using KnockMongoOptions = identity::Knock.Identity.Api.Hosting.KnockMongoOptions;

namespace Knock.DatabaseTest.Demo;

/// <summary>Spec 025, T-08: la verificación decidida de la demo (<c>verificaciones.json</c>) es ficticia, sin CURP, idempotente y con las mismas guardas que las cuentas demo.</summary>
public sealed class DemoVerificationSeedTests
{
    private static readonly string _path = Path.Combine(CatalogTestDatabase.SeedsDirectory, "demo", "identity", "verificaciones.json");
    private static readonly string _usersPath = Path.Combine(CatalogTestDatabase.SeedsDirectory, "demo", "identity", "usuarios.json");

    [Fact]
    public void SeedFile_Is_Fictitious_Verified_And_References_Demo_Clients_CA_025_14()
    {
        using var seed = JsonDocument.Parse(File.ReadAllText(_path));
        using var users = JsonDocument.Parse(File.ReadAllText(_usersPath));
        var userIds = users.RootElement.EnumerateArray().Select(x => x.GetProperty("_id").GetProperty("$oid").GetString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in users.RootElement.EnumerateArray())
        {
            ids.Add(user.GetProperty("_id").GetProperty("$oid").GetString()!);
        }

        Assert.NotEqual(0, seed.RootElement.GetArrayLength());
        foreach (var entry in seed.RootElement.EnumerateArray())
        {
            Assert.True(ids.Add(entry.GetProperty("_id").GetProperty("$oid").GetString()!));
            Assert.Contains(entry.GetProperty("usuario").GetProperty("$oid").GetString(), userIds);
            Assert.Equal("verificada", entry.GetProperty("estado").GetString());
            Assert.True(entry.GetProperty("demo").GetBoolean());
            Assert.False(entry.TryGetProperty("curp", out _) || entry.TryGetProperty("curpHmac", out _));
        }

        Assert.DoesNotContain("curp", File.ReadAllText(_path), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Seeds_Verified_Verification_And_Consent_Idempotently_CA_025_14()
    {
        await WithDatabase(async (database, seeder, sub) =>
        {
            await seeder.SeedAsync(_path, CancellationToken.None);
            await seeder.SeedAsync(_path, CancellationToken.None);
            var verification = await database.GetCollection<BsonDocument>("verificaciones").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
            var consent = await database.GetCollection<BsonDocument>("consentimientos").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
            Assert.Equal(sub, verification["usuarioId"].AsString);
            Assert.Equal("verificada", verification["estado"].AsString);
            Assert.Equal(verification["nivel"].AsString, verification["nivelOtorgado"].AsString);
            Assert.False(verification["curpVigente"].AsBoolean);
            Assert.All(verification["fotos"].AsBsonArray, photo => Assert.Equal("borrada", photo["estado"].AsString));
            Assert.Equal(verification["_id"], consent["verificacionId"]);
            Assert.DoesNotContain("curp", verification.ToJson().Replace("curpVigente", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Missing_Demo_Account_Writes_Nothing_CA_025_14()
    {
        await WithDatabase(async (database, seeder, _) =>
        {
            await database.GetCollection<BsonDocument>("usuarios").DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
            await seeder.SeedAsync(_path, CancellationToken.None);
            Assert.Equal(0, await database.GetCollection<BsonDocument>("verificaciones").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Equal(0, await database.GetCollection<BsonDocument>("consentimientos").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        });
    }

    [Theory]
    [InlineData(false, "Development", null)]
    [InlineData(true, "Production", null)]
    [InlineData(true, "Development", "mongodb://remote.invalid")]
    public async Task Disabled_Production_Or_Remote_Host_Rejects_Before_Writing_CA_025_14(bool enabled, string environment, string? connection)
    {
        await WithDatabase(async (database, seeder, _) =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync(_path, CancellationToken.None));
            Assert.Equal(0, await database.GetCollection<BsonDocument>("verificaciones").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        }, enabled, environment, connection);
    }

    [Fact]
    public async Task NonDemo_User_Rejects_Before_Writing_CA_025_14()
    {
        await WithDatabase(async (database, seeder, _) =>
        {
            await database.GetCollection<BsonDocument>("usuarios").InsertOneAsync(new BsonDocument { ["_id"] = ObjectId.GenerateNewId() });
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync(_path, CancellationToken.None));
            Assert.Contains("non-demo", error.Message, StringComparison.Ordinal);
            Assert.Equal(0, await database.GetCollection<BsonDocument>("verificaciones").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        });
    }

    [Fact]
    public async Task Invalid_File_Writes_Nothing_CA_025_14()
    {
        await WithDatabase(async (database, seeder, _) =>
        {
            var seeds = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(_path))!.AsArray();
            seeds[0]!["estado"] = "en_revision";
            var temporary = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(temporary, seeds.ToJsonString());
                await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync(temporary, CancellationToken.None));
                Assert.Equal(0, await database.GetCollection<BsonDocument>("verificaciones").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            }
            finally { File.Delete(temporary); }
        });
    }

    private static async Task WithDatabase(Func<IMongoDatabase, DemoVerificationSeeder, string, Task> test, bool enabled = true, string environment = "Development", string? connection = null)
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        using var client = new MongoClient(fixture.ConnectionString);
        var name = "s025_demo_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
        try
        {
            var database = client.GetDatabase(name);
            var sub = Guid.NewGuid().ToString("D");
            await database.GetCollection<BsonDocument>("usuarios").InsertOneAsync(new BsonDocument { ["_id"] = ObjectId.Parse("de0000000000000000000001"), ["sub"] = sub, ["demo"] = true });
            var store = new DemoVerificationStore(database, Options.Create(new KnockMongoOptions { ConnectionString = connection ?? fixture.ConnectionString }));
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:Demo"] = enabled.ToString() }).Build();
            await test(database, new DemoVerificationSeeder(store, configuration, new DemoFakeEnvironment { EnvironmentName = environment }, NullLogger<DemoVerificationSeeder>.Instance), sub);
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }
}
