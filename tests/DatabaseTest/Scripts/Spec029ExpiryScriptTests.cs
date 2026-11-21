using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.DatabaseTest.Support;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.DatabaseTest.Scripts;

/// <summary>Spec 029, T-06 (R-10, CA-09): <c>db/029-requests-vencimiento.js</c> programa una sola tarea <c>BarrerVencidas</c> y es idempotente.</summary>
public sealed class Spec029ExpiryScriptTests
{
    [Fact]
    public async Task Script_Schedules_The_Sweep_Once_And_Reapplying_Keeps_It_CA_029_09()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        var name = "s029_sweep_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        using var client = new MongoClient(documentDb.ConnectionString);
        try
        {
            await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_MARKET"] = name });
            var script = $"{MongoToolsContainer.DbDirectory}/029-requests-vencimiento.js";

            (await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script)).ExitCode.Should().Be(0);
            (await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script)).ExitCode.Should().Be(0);

            var database = client.GetDatabase(name);
            (await database.GetCollection<BsonDocument>("tareas_programadas").CountDocumentsAsync(new BsonDocument("tipo", "BarrerVencidas"))).Should().Be(1);
            (await MongoIndexes.DescribeAsync(database, "solicitudes")).Should().Contain("estado_1_venceEn_1 estado:1,venceEn:1 - ttl:-");
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }
}
