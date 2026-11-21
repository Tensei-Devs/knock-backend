using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.DatabaseTest.Support;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.DatabaseTest.Scripts;

/// <summary>Spec 053, T-01 (§6): <c>db/053-panel-de-revision.js</c> crea el índice de la cola de verificaciones de forma idempotente y sin borrar datos.</summary>
public sealed class Spec053PanelScriptTests
{
    [Fact]
    public async Task Script_Crea_El_Indice_De_La_Cola_Idempotente_CA_053_05()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        var name = "s053_panel_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        using var client = new MongoClient(documentDb.ConnectionString);
        try
        {
            await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_IDENTITY"] = name });
            var script = $"{MongoToolsContainer.DbDirectory}/053-panel-de-revision.js";

            var first = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);
            first.ExitCode.Should().Be(0, first.Output);
            var identity = client.GetDatabase(name);
            var verifications = identity.GetCollection<BsonDocument>("verificaciones");
            await verifications.InsertOneAsync(new BsonDocument { ["usuarioId"] = "ficticio-1", ["estado"] = "en_revision", ["enviadoEn"] = DateTime.UtcNow });
            var second = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);

            second.ExitCode.Should().Be(0, $"reaplicar el script no falla: {second.Output}");
            (await verifications.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(1, "reaplicar no borra datos");
            (await MongoIndexes.DescribeAsync(identity, "verificaciones")).Should().Equal(
                "_id_ _id:1 - ttl:-",
                "estado_1_enviadoEn_1 estado:1,enviadoEn:1 - ttl:-");
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }
}
