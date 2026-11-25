using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.DatabaseTest.Support;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.DatabaseTest.Scripts;

/// <summary>
/// Spec 044, T-01 (§6): <c>db/044-avisos-dentro-de-la-app.js</c> crea <c>avisos</c> y <c>preferencias_avisos</c> en la base de Mensajería con sus índices (lista y contador, TTL por
/// <c>venceEn</c> y una preferencia por persona); reaplicarlo no cambia nada ni borra documentos.
/// </summary>
public sealed class Spec044AvisosIndicesTests
{
    private const string _scriptName = "044-avisos-dentro-de-la-app.js";

    [Fact]
    public async Task Script_CreatesTheCollectionsAndIndexes_AndIsIdempotent_CA_044_20()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        var name = "s044_idx_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        using var client = new MongoClient(documentDb.ConnectionString);
        try
        {
            await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_MESSAGING"] = name });
            var script = $"{MongoToolsContainer.DbDirectory}/{_scriptName}";

            var applied = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);

            applied.ExitCode.Should().Be(0, applied.Output);
            var database = client.GetDatabase(name);
            var notices = database.GetCollection<BsonDocument>("avisos");
            await notices.InsertOneAsync(new BsonDocument { ["usuarioId"] = "u1", ["leido"] = false, ["creadoEn"] = DateTime.UtcNow });
            var again = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);

            again.ExitCode.Should().Be(0, $"reaplicar el script no falla: {again.Output}");
            (await notices.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(1, "reaplicar no borra datos");
            (await MongoIndexes.DescribeAsync(database, "avisos")).Should().Equal(
                "_id_ _id:1 - ttl:-",
                "eventoId_1_usuarioId_1 eventoId:1,usuarioId:1 unique ttl:-",
                "usuarioId_1_leido_1_creadoEn_-1 usuarioId:1,leido:1,creadoEn:-1 - ttl:-",
                "venceEn_1 venceEn:1 - ttl:0");
            (await MongoIndexes.DescribeAsync(database, "preferencias_avisos")).Should().Equal(
                "_id_ _id:1 - ttl:-",
                "usuarioId_1 usuarioId:1 unique ttl:-");
            using var names = await database.ListCollectionNamesAsync();
            (await names.ToListAsync()).Order(StringComparer.Ordinal).Should().Equal("avisos", "preferencias_avisos");
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }

    [Fact]
    public async Task Script_RejectsASecondPreferencesDocumentForTheSamePerson_CA_044_24()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        var name = "s044_pre_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        using var client = new MongoClient(documentDb.ConnectionString);
        try
        {
            await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_MESSAGING"] = name });
            (await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", $"{MongoToolsContainer.DbDirectory}/{_scriptName}")).ExitCode.Should().Be(0);
            var preferences = client.GetDatabase(name).GetCollection<BsonDocument>("preferencias_avisos");
            await preferences.InsertOneAsync(new BsonDocument { ["usuarioId"] = "u1" });

            var duplicate = async () => await preferences.InsertOneAsync(new BsonDocument { ["usuarioId"] = "u1" });

            (await duplicate.Should().ThrowAsync<MongoWriteException>()).Which.WriteError.Category.Should().Be(ServerErrorCategory.DuplicateKey);
            await preferences.InsertOneAsync(new BsonDocument { ["usuarioId"] = "u2" });
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }

    [Fact]
    public async Task Script_MakesTheInAppNoticeIdempotentPerEventAndPerson_R_14()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        var name = "s044_evt_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        using var client = new MongoClient(documentDb.ConnectionString);
        try
        {
            await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_MESSAGING"] = name });
            (await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", $"{MongoToolsContainer.DbDirectory}/{_scriptName}")).ExitCode.Should().Be(0);
            var notices = client.GetDatabase(name).GetCollection<BsonDocument>("avisos");
            await notices.InsertManyAsync([new BsonDocument { ["usuarioId"] = "u1" }, new BsonDocument { ["usuarioId"] = "u1" }]);
            await notices.InsertOneAsync(new BsonDocument { ["usuarioId"] = "u1", ["eventoId"] = "e1" });

            var duplicate = async () => await notices.InsertOneAsync(new BsonDocument { ["usuarioId"] = "u1", ["eventoId"] = "e1" });

            (await duplicate.Should().ThrowAsync<MongoWriteException>()).Which.WriteError.Category.Should().Be(ServerErrorCategory.DuplicateKey);
            await notices.InsertOneAsync(new BsonDocument { ["usuarioId"] = "u2", ["eventoId"] = "e1" });
            await notices.InsertOneAsync(new BsonDocument { ["usuarioId"] = "u1", ["eventoId"] = "e2" });
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }

    [Theory]
    [InlineData("nombre con espacios")]
    [InlineData("a;b")]
    public async Task Script_RejectsAnInvalidDatabaseName_CA_044_24(string name)
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_MESSAGING"] = name });

        var result = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", $"{MongoToolsContainer.DbDirectory}/{_scriptName}");

        result.ExitCode.Should().NotBe(0);
    }
}
