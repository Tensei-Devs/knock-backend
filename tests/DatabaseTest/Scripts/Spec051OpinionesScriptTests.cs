using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.DatabaseTest.Support;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.DatabaseTest.Scripts;

/// <summary>Spec 051, T-01 (§6): <c>db/051-opiniones-y-reputacion.js</c> crea <c>opiniones</c> con sus índices y es idempotente.</summary>
public sealed class Spec051OpinionesScriptTests
{
    [Fact]
    public async Task Script_Crea_Coleccion_E_Indices_De_Forma_Idempotente_CA_051_28()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        var name = "s051_opi_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        using var client = new MongoClient(documentDb.ConnectionString);
        try
        {
            await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_MARKET"] = name });
            var script = $"{MongoToolsContainer.DbDirectory}/051-opiniones-y-reputacion.js";

            var first = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);
            var market = client.GetDatabase(name);
            var services = market.GetCollection<BsonDocument>("opiniones");
            await services.InsertOneAsync(new BsonDocument { ["visitaId"] = ObjectId.GenerateNewId(), ["tecnicoId"] = ObjectId.GenerateNewId(), ["calificacion"] = 5, ["creadoEn"] = DateTime.UtcNow });
            var second = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);

            first.ExitCode.Should().Be(0, first.Output);
            second.ExitCode.Should().Be(0, second.Output);
            (await services.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(1);
            var indexes = await MongoIndexes.DescribeAsync(market, "opiniones");
            indexes.Should().Contain("visitaId_1 visitaId:1 unique ttl:-");
            indexes.Should().Contain("tecnicoId_1_creadoEn_-1 tecnicoId:1,creadoEn:-1 - ttl:-");
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }
}
