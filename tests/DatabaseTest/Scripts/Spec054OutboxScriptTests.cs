using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.DatabaseTest.Support;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.DatabaseTest.Scripts;

/// <summary>Spec 054, T-01 (§6): <c>db/054-outbox-y-tareas-programadas.js</c> crea los índices de las dos colecciones técnicas en las tres bases, de forma idempotente y sin borrar datos.</summary>
public sealed class Spec054OutboxScriptTests
{
    [Fact]
    public async Task Script_Crea_Indices_Unico_Y_Ttl_Idempotente_CA_054_09()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        var suffix = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        string[] names = ["s054_identity_" + suffix, "s054_market_" + suffix, "s054_messaging_" + suffix];
        using var client = new MongoClient(documentDb.ConnectionString);
        try
        {
            await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string>
            {
                ["KNOCK_DB_IDENTITY"] = names[0],
                ["KNOCK_DB_MARKET"] = names[1],
                ["KNOCK_DB_MESSAGING"] = names[2],
            });
            var script = $"{MongoToolsContainer.DbDirectory}/054-outbox-y-tareas-programadas.js";

            var first = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);
            first.ExitCode.Should().Be(0, first.Output);
            var tasks = client.GetDatabase(names[0]).GetCollection<BsonDocument>("tareas_programadas");
            await tasks.InsertOneAsync(new BsonDocument { ["tipo"] = "VencerSolicitud", ["claveNegocio"] = "ficticio-1", ["estado"] = "pendiente" });
            var second = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);

            second.ExitCode.Should().Be(0, $"reaplicar el script no falla: {second.Output}");
            (await tasks.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(1, "reaplicar no borra datos");
            var duplicate = () => tasks.InsertOneAsync(new BsonDocument { ["tipo"] = "VencerSolicitud", ["claveNegocio"] = "ficticio-1", ["estado"] = "pendiente" });
            (await duplicate.Should().ThrowAsync<MongoWriteException>()).Which.WriteError.Category.Should().Be(ServerErrorCategory.DuplicateKey);
            foreach (var name in names)
            {
                var database = client.GetDatabase(name);
                (await MongoIndexes.DescribeAsync(database, "outbox")).Should().Equal(
                    "_id_ _id:1 - ttl:-",
                    "estado_1_programadaPara_1 estado:1,programadaPara:1 - ttl:-",
                    "limpiarEn_1 limpiarEn:1 - ttl:0");
                (await MongoIndexes.DescribeAsync(database, "tareas_programadas")).Should().Equal(
                    "_id_ _id:1 - ttl:-",
                    "estado_1_programadaPara_1 estado:1,programadaPara:1 - ttl:-",
                    "limpiarEn_1 limpiarEn:1 - ttl:0",
                    "tipo_1_claveNegocio_1 tipo:1,claveNegocio:1 unique ttl:-");
            }
        }
        finally
        {
            foreach (var name in names)
            {
                await client.DropDatabaseAsync(name);
            }
        }
    }
}
