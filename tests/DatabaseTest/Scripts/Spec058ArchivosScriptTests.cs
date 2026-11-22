using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.DatabaseTest.Support;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.DatabaseTest.Scripts;

/// <summary>Spec 058, T-01 (R-01, R-03, R-07): <c>db/058-market-archivos.js</c> crea <c>archivos</c> con sus índices, es idempotente y el índice compuesto sirve al conteo de pendientes.</summary>
public sealed class Spec058ArchivosScriptTests
{
    private const string _script = "058-market-archivos.js";

    [Fact]
    public async Task Script_Crea_Indices_De_Forma_Idempotente_CA_058_01()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        var name = "s058_arch_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        using var client = new MongoClient(documentDb.ConnectionString);
        try
        {
            await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_MARKET"] = name });
            var script = $"{MongoToolsContainer.DbDirectory}/{_script}";

            (await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script)).ExitCode.Should().Be(0);
            var archivos = client.GetDatabase(name).GetCollection<BsonDocument>("archivos");
            await archivos.InsertOneAsync(new BsonDocument { ["archivoId"] = "a", ["propietarioId"] = "p", ["estado"] = "pendiente", ["confirmarAntesDe"] = DateTime.UtcNow.AddMinutes(30) });
            var otra = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);

            otra.ExitCode.Should().Be(0, otra.Output);
            (await archivos.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(1);
            (await MongoIndexes.DescribeAsync(client.GetDatabase(name), "archivos")).Should().Equal(
                "_id_ _id:1 - ttl:-",
                "archivoId_1 archivoId:1 unique ttl:-",
                "expiraEn_1 expiraEn:1 - ttl:0",
                "propietarioId_1_estado_1_confirmarAntesDe_1 propietarioId:1,estado:1,confirmarAntesDe:1 - ttl:-");
            var duplicado = async () => await archivos.InsertOneAsync(new BsonDocument { ["archivoId"] = "a" });
            await duplicado.Should().ThrowAsync<MongoWriteException>("archivoId es único");
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }
}
