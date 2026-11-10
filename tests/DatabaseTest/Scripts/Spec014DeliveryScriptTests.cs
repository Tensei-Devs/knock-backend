using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.DatabaseTest.Support;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.DatabaseTest.Scripts;

/// <summary>
/// Spec 014, CA-01 (R-02): <c>db/entrega.js</c> sobre una instancia vacía deja las colecciones, los índices y los datos de ejemplo, y reaplicarlo no
/// duplica nada. Corre el script de verdad con <c>mongosh</c> contra documentdb-local. Solo datos ficticios.
/// </summary>
public sealed class Spec014DeliveryScriptTests
{
    private const int _oficios = 3;
    private const int _colonias = 13;

    [Fact]
    public async Task DeliveryScript_OnAnEmptyInstance_LeavesCollectionsIndexesAndSampleData_AndReapplyingChangesNothing_CA_014_01()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        var suffix = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        var names = new Dictionary<string, string>
        {
            ["KNOCK_DB_MARKET"] = "s014_market_" + suffix,
            ["KNOCK_DB_IDENTITY"] = "s014_identity_" + suffix,
            ["KNOCK_DB_MESSAGING"] = "s014_messaging_" + suffix,
            ["KNOCK_DB_DIR"] = MongoToolsContainer.DbDirectory,
        };
        using var client = new MongoClient(documentDb.ConnectionString);
        try
        {
            var market = client.GetDatabase(names["KNOCK_DB_MARKET"]);
            (await (await market.ListCollectionNamesAsync()).ToListAsync()).Should().BeEmpty("la instancia empieza vacía");
            await using var tools = await MongoToolsContainer.StartAsync(documentDb, names);
            var script = $"{MongoToolsContainer.DbDirectory}/entrega.js";

            var first = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);

            first.ExitCode.Should().Be(0, first.Output);
            first.Output.Should().Contain("Entrega de la base lista").And.NotContain("Kn0ck", "la salida nunca muestra la contraseña");
            (await market.GetCollection<BsonDocument>("oficios").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(_oficios);
            (await market.GetCollection<BsonDocument>("colonias").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(_colonias);
            (await MongoIndexes.DescribeAsync(market, "oficios")).Should().Contain("clave_1 clave:1 unique ttl:-");
            (await MongoIndexes.DescribeAsync(market, "colonias")).Should().Contain("municipio_1_cp_1_nombre_1 municipio:1,cp:1,nombre:1 unique ttl:-");
            foreach (var database in new[] { names["KNOCK_DB_IDENTITY"], names["KNOCK_DB_MESSAGING"] })
            {
                var collections = await (await client.GetDatabase(database).ListCollectionNamesAsync()).ToListAsync();
                collections.Should().Contain(["outbox", "tareas_programadas"], database);
            }

            var identityCollections = await (await client.GetDatabase(names["KNOCK_DB_IDENTITY"]).ListCollectionNamesAsync()).ToListAsync();
            identityCollections.Should().Contain(["usuarios", "sesiones"], "el script 002 de la spec 015 también corre en la entrega");

            var electricity = await market.GetCollection<BsonDocument>("oficios").Find(new BsonDocument("clave", "ele")).SingleAsync();
            electricity["_id"].AsObjectId.ToString().Should().Be("6adfe9802fb81ddc3fc17f73", "las semillas conservan sus _id fijos");
            var withPolygon = await market.GetCollection<BsonDocument>("colonias").CountDocumentsAsync(new BsonDocument("geom", new BsonDocument("$exists", true)));
            withPolygon.Should().Be(9, "nueve colonias traen su polígono; las otras cuatro solo se ven por código postal");
            var before = await SnapshotAsync(market);

            var second = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);

            second.ExitCode.Should().Be(0, $"reaplicar el script no falla: {second.Output}");
            (await SnapshotAsync(market)).Should().Equal(before, "reaplicar no duplica ni cambia los datos");
        }
        finally
        {
            foreach (var database in new[] { names["KNOCK_DB_MARKET"], names["KNOCK_DB_IDENTITY"], names["KNOCK_DB_MESSAGING"] })
            {
                await client.DropDatabaseAsync(database);
            }
        }
    }

    [Fact]
    public async Task DeliveryScript_WithoutTheConnectionString_Stops_CA_014_01()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_DIR"] = MongoToolsContainer.DbDirectory });

        var result = await tools.RunAsync("sh", "-c", $"env -u KNOCK_DB_URI mongosh --nodb --quiet --file {MongoToolsContainer.DbDirectory}/entrega.js");

        result.ExitCode.Should().Be(2);
        result.Output.Should().Contain("KNOCK_DB_URI");
    }

    private static async Task<List<string>> SnapshotAsync(IMongoDatabase database)
    {
        var all = new List<string>();
        foreach (var collection in new[] { "oficios", "colonias" })
        {
            var documents = await database.GetCollection<BsonDocument>(collection).Find(FilterDefinition<BsonDocument>.Empty).SortBy(d => d["_id"]).ToListAsync();
            all.AddRange(documents.Select(d => collection + " " + d.ToJson()));
        }

        return all;
    }
}
