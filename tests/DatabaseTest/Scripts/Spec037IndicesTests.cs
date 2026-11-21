using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.DatabaseTest.Support;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.DatabaseTest.Scripts;

/// <summary>
/// Spec 037, T-01 (R-01, R-02, R-06, R-07, R-12): <c>db/037-notificaciones.js</c> crea <c>notifications.dispositivos</c>, <c>notifications.enviados</c> y <c>notifications.pendientes</c>
/// en la base de Mensajería con sus índices (token único, deduplicación por evento y persona, ventana única y TTL), reaplicarlo no cambia nada ni borra documentos.
/// </summary>
public sealed class Spec037IndicesTests
{
    private const string _scriptName = "037-notificaciones.js";

    [Fact]
    public async Task Script_CreatesTheCollectionsAndIndexes_AndIsIdempotent_R_06()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        var name = "s037_idx_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        using var client = new MongoClient(documentDb.ConnectionString);
        try
        {
            await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_MESSAGING"] = name });
            var script = $"{MongoToolsContainer.DbDirectory}/{_scriptName}";

            var applied = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);

            applied.ExitCode.Should().Be(0, applied.Output);
            var database = client.GetDatabase(name);
            var devices = database.GetCollection<BsonDocument>("notifications.dispositivos");
            await devices.InsertOneAsync(new BsonDocument { ["tokenHuella"] = "h1", ["personaId"] = "p1", ["renovadoEn"] = DateTime.UtcNow });
            var again = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);

            again.ExitCode.Should().Be(0, $"reaplicar el script no falla: {again.Output}");
            (await devices.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(1, "reaplicar no borra datos");
            (await MongoIndexes.DescribeAsync(database, "notifications.dispositivos")).Should().Equal(
                "_id_ _id:1 - ttl:-",
                "personaId_1_plataforma_1 personaId:1,plataforma:1 - ttl:-",
                "renovadoEn_1 renovadoEn:1 - ttl:2592000",
                "sid_1 sid:1 - ttl:-",
                "tokenHuella_1 tokenHuella:1 unique ttl:-");
            (await MongoIndexes.DescribeAsync(database, "notifications.enviados")).Should().Equal(
                "_id_ _id:1 - ttl:-",
                "creadoEn_1 creadoEn:1 - ttl:2592000",
                "destinatario_1_creadoEn_-1 destinatario:1,creadoEn:-1 - ttl:-",
                "eventoId_1_destinatario_1 eventoId:1,destinatario:1 unique ttl:-");
            (await MongoIndexes.DescribeAsync(database, "notifications.pendientes")).Should().Equal(
                "_id_ _id:1 - ttl:-",
                "cierraEn_1 cierraEn:1 - ttl:-",
                "conversacionId_1_destinatario_1 conversacionId:1,destinatario:1 unique ttl:-",
                "creadoEn_1 creadoEn:1 - ttl:86400");
            using var names = await database.ListCollectionNamesAsync();
            (await names.ToListAsync()).Order(StringComparer.Ordinal).Should().Equal("notifications.dispositivos", "notifications.enviados", "notifications.pendientes");
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }

    [Fact]
    public async Task Script_RejectsTheSameTokenTwiceAndTheSameEventForTheSamePerson_R_01_R_06()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        var name = "s037_uni_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        using var client = new MongoClient(documentDb.ConnectionString);
        try
        {
            await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_MESSAGING"] = name });
            (await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", $"{MongoToolsContainer.DbDirectory}/{_scriptName}")).ExitCode.Should().Be(0);
            var database = client.GetDatabase(name);
            var devices = database.GetCollection<BsonDocument>("notifications.dispositivos");
            var sent = database.GetCollection<BsonDocument>("notifications.enviados");
            await devices.InsertOneAsync(new BsonDocument { ["tokenHuella"] = "h1", ["personaId"] = "p1" });
            await sent.InsertOneAsync(new BsonDocument { ["eventoId"] = "e1", ["destinatario"] = "p1" });

            var sameToken = async () => await devices.InsertOneAsync(new BsonDocument { ["tokenHuella"] = "h1", ["personaId"] = "p2" });
            var sameEvent = async () => await sent.InsertOneAsync(new BsonDocument { ["eventoId"] = "e1", ["destinatario"] = "p1" });

            (await sameToken.Should().ThrowAsync<MongoWriteException>()).Which.WriteError.Category.Should().Be(ServerErrorCategory.DuplicateKey);
            (await sameEvent.Should().ThrowAsync<MongoWriteException>()).Which.WriteError.Category.Should().Be(ServerErrorCategory.DuplicateKey);
            await sent.InsertOneAsync(new BsonDocument { ["eventoId"] = "e1", ["destinatario"] = "p2" });
            await sent.InsertOneAsync(new BsonDocument { ["eventoId"] = "e2", ["destinatario"] = "p1" });
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }

    [Theory]
    [InlineData("nombre con espacios")]
    [InlineData("a;b")]
    public async Task Script_RejectsAnInvalidDatabaseName_R_01(string name)
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_MESSAGING"] = name });

        var result = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", $"{MongoToolsContainer.DbDirectory}/{_scriptName}");

        result.ExitCode.Should().NotBe(0);
    }
}
