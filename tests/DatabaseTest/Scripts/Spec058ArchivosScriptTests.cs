using System.Diagnostics;
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
    public async Task Script_Crea_Indices_De_Forma_Idempotente_CA_058_01() =>
        await WithScriptAsync(async (client, name, tools) =>
        {
            var script = $"{MongoToolsContainer.DbDirectory}/{_script}";
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
        });

    [Fact]
    public async Task Ttl_Borra_Pendientes_Y_Rechazados_No_Listos_CA_058_04() =>
        await WithScriptAsync(async (client, name, _) =>
        {
            var archivos = client.GetDatabase(name).GetCollection<BsonDocument>("archivos");
            var vencido = DateTime.UtcNow.AddDays(-1);
            var listo = new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc);
            await archivos.InsertManyAsync(
            [
                new BsonDocument { ["archivoId"] = "pending", ["estado"] = "pendiente", ["expiraEn"] = vencido },
                new BsonDocument { ["archivoId"] = "rejected", ["estado"] = "rechazado", ["expiraEn"] = vencido },
                new BsonDocument { ["archivoId"] = "ready", ["estado"] = "listo", ["expiraEn"] = listo },
            ]);

            // The server runs TTL cleanup asynchronously.
            var elapsed = Stopwatch.StartNew();
            List<BsonDocument> remaining;
            while (true)
            {
                remaining = await archivos.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
                if (remaining.Count <= 1 || elapsed.Elapsed >= TimeSpan.FromMinutes(3))
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            remaining.Select(file => file["archivoId"].AsString).Should().Equal("ready");
        });

    /// <summary>CA-20: el conteo de pendientes vigentes de una persona (el de <c>MongoMediaFileStore</c>) se resuelve con el índice compuesto y no recorre toda la colección.</summary>
    [Fact]
    public async Task Conteo_Usa_Indice_Compuesto_CA_058_20() =>
        await WithScriptAsync(async (client, name, _) =>
        {
            var database = client.GetDatabase(name);
            await database.GetCollection<BsonDocument>("archivos").InsertManyAsync(Enumerable.Range(0, 300).Select(n => new BsonDocument
            {
                ["archivoId"] = $"id-{n}",
                ["propietarioId"] = $"owner-{n % 30}",
                ["estado"] = n % 3 == 0 ? "pendiente" : "listo",
                ["confirmarAntesDe"] = DateTime.UtcNow.AddMinutes(n),
            }));
            var count = new BsonDocument
            {
                { "count", "archivos" },
                { "query", new BsonDocument { { "propietarioId", "owner-3" }, { "estado", "pendiente" }, { "confirmarAntesDe", new BsonDocument("$gt", DateTime.UtcNow) } } },
            };

            var explain = await database.RunCommandAsync<BsonDocument>(new BsonDocument { { "explain", count }, { "verbosity", "queryPlanner" } });

            var plan = explain.ToJson();
            plan.Should().Contain("propietarioId_1_estado_1_confirmarAntesDe_1", "el conteo usa el índice compuesto");
            plan.Should().NotContain("COLLSCAN", "no recorre toda la colección");
        });

    private static async Task WithScriptAsync(Func<MongoClient, string, MongoToolsContainer, Task> assertions)
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        var name = "s058_arch_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        using var client = new MongoClient(documentDb.ConnectionString);
        try
        {
            await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_MARKET"] = name });
            (await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", $"{MongoToolsContainer.DbDirectory}/{_script}")).ExitCode.Should().Be(0);
            await assertions(client, name, tools);
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }
}
