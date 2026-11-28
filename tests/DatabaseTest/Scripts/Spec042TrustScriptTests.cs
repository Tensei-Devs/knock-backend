using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.DatabaseTest.Support;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.DatabaseTest.Scripts;

/// <summary>
/// Spec 042, T-01 (§6): <c>db/042-tecnicos-de-confianza.js</c> crea <c>tecnicos_confianza</c> en la base de Mercado con el índice único (clienteId, tecnicoId), reaplicarlo
/// no cambia nada ni borra documentos, el índice rechaza el par repetido y no toca otras colecciones.
/// </summary>
public sealed class Spec042TrustScriptTests
{
    [Fact]
    public async Task Script_CreatesTheCollectionAndItsUniqueIndexIdempotently_AndKeepsTheDocuments_R_03()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        var name = "s042_conf_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        using var client = new MongoClient(documentDb.ConnectionString);
        try
        {
            await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_MARKET"] = name });
            var script = $"{MongoToolsContainer.DbDirectory}/042-tecnicos-de-confianza.js";

            var applied = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);

            applied.ExitCode.Should().Be(0, applied.Output);
            var database = client.GetDatabase(name);
            var trust = database.GetCollection<BsonDocument>("tecnicos_confianza");
            var customerId = ObjectId.GenerateNewId();
            var technicianId = ObjectId.GenerateNewId();
            await trust.InsertOneAsync(new BsonDocument { ["clienteId"] = customerId, ["tecnicoId"] = technicianId, ["guardadoEn"] = DateTime.UtcNow });
            var again = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);

            again.ExitCode.Should().Be(0, $"reaplicar el script no falla: {again.Output}");
            (await trust.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(1, "reaplicar no borra datos");
            (await MongoIndexes.DescribeAsync(database, "tecnicos_confianza")).Should().Equal(
                "_id_ _id:1 - ttl:-",
                "clienteId_1_tecnicoId_1 clienteId:1,tecnicoId:1 unique ttl:-");
            var duplicate = () => trust.InsertOneAsync(new BsonDocument { ["clienteId"] = customerId, ["tecnicoId"] = technicianId });
            (await duplicate.Should().ThrowAsync<MongoWriteException>()).Which.WriteError.Category.Should().Be(ServerErrorCategory.DuplicateKey);
            await trust.InsertOneAsync(new BsonDocument { ["clienteId"] = customerId, ["tecnicoId"] = ObjectId.GenerateNewId() });
            using var names = await database.ListCollectionNamesAsync();
            (await names.ToListAsync()).Should().Equal("tecnicos_confianza");
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }

    [Theory]
    [InlineData("nombre con espacios")]
    [InlineData("a;b")]
    public async Task Script_RejectsAnInvalidDatabaseName_R_03(string name)
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_MARKET"] = name });

        var result = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", $"{MongoToolsContainer.DbDirectory}/042-tecnicos-de-confianza.js");

        result.ExitCode.Should().NotBe(0);
    }
}
