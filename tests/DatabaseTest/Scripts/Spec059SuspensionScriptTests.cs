using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.DatabaseTest.Support;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.DatabaseTest.Scripts;

/// <summary>Spec 059, T-01 (§6): <c>db/059-suspension-cuenta.js</c> documenta <c>usuarios.suspension</c> sin cambiar datos ni agregar índices, y es idempotente.</summary>
public sealed class Spec059SuspensionScriptTests
{
    [Fact]
    public async Task Script_Es_Idempotente_No_Cambia_Documentos_Ni_Agrega_Indices_CA_059_T01()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        var name = "s059_identity_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        using var client = new MongoClient(documentDb.ConnectionString);
        try
        {
            await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_IDENTITY"] = name });
            var script = $"{MongoToolsContainer.DbDirectory}/059-suspension-cuenta.js";
            var users = client.GetDatabase(name).GetCollection<BsonDocument>("usuarios");
            var reportId = ObjectId.GenerateNewId();
            await users.InsertManyAsync(
            [
                new BsonDocument { ["sub"] = "ficticio-activa", ["estado"] = "activa" },
                new BsonDocument
                {
                    ["sub"] = "ficticio-suspendida",
                    ["estado"] = "suspendida",
                    ["suspension"] = new BsonDocument { ["tipo"] = "preventiva", ["reporteId"] = reportId, ["desde"] = DateTime.UtcNow },
                },
            ]);
            var indexesBefore = (await (await users.Indexes.ListAsync()).ToListAsync()).Count;

            var first = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);
            var second = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);

            first.ExitCode.Should().Be(0, first.Output);
            second.ExitCode.Should().Be(0, $"reaplicar el script no falla: {second.Output}");
            (await users.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(2);
            var active = await users.Find(Builders<BsonDocument>.Filter.Eq("sub", "ficticio-activa")).SingleAsync();
            active.Contains("suspension").Should().BeFalse("el script no escribe el campo: solo lo escribe el servicio");
            var suspended = await users.Find(Builders<BsonDocument>.Filter.Eq("sub", "ficticio-suspendida")).SingleAsync();
            suspended["suspension"]["reporteId"].AsObjectId.Should().Be(reportId);
            (await (await users.Indexes.ListAsync()).ToListAsync()).Should().HaveCount(indexesBefore, "no hay consulta por suspension, así que no hay índice nuevo");
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }
}
