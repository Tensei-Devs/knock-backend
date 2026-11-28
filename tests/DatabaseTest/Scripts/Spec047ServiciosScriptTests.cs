using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.DatabaseTest.Support;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.DatabaseTest.Scripts;

/// <summary>Spec 047, T-01 (§6): <c>db/047-servicios-tecnico.js</c> crea <c>servicios_tecnico</c> con sus índices y es idempotente.</summary>
public sealed class Spec047ServiciosScriptTests
{
    [Fact]
    public async Task Script_Crea_Coleccion_E_Indices_De_Forma_Idempotente_CA_047_01()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        var name = "s047_serv_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        using var client = new MongoClient(documentDb.ConnectionString);
        try
        {
            await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_MARKET"] = name });
            var script = $"{MongoToolsContainer.DbDirectory}/047-servicios-tecnico.js";

            var first = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);
            var market = client.GetDatabase(name);
            var services = market.GetCollection<BsonDocument>("servicios_tecnico");
            await services.InsertOneAsync(new BsonDocument { ["tecnicoId"] = ObjectId.GenerateNewId(), ["nombre"] = "Cambio de llave", ["precioReferencia"] = 350, ["oficio"] = "plo" });
            var second = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);

            first.ExitCode.Should().Be(0, first.Output);
            second.ExitCode.Should().Be(0, second.Output);
            (await services.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(1);
            var indexes = await MongoIndexes.DescribeAsync(market, "servicios_tecnico");
            indexes.Should().Contain("tecnicoId_1_oficio_1 tecnicoId:1,oficio:1 - ttl:-");
            indexes.Should().Contain(index => index.StartsWith("nombre_text ", StringComparison.Ordinal));
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }
}
