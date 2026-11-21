using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.DatabaseTest.Support;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.DatabaseTest.Scripts;

/// <summary>Spec 057, T1: <c>db/057-identity-verificaciones-purga.js</c> completa con null los campos de la purga, sin cambiar los que ya existen ni borrar datos (idempotente).</summary>
public sealed class Spec057PurgaScriptTests
{
    [Fact]
    public async Task Script_Completa_Campos_Sin_Cambiar_Los_Existentes_Idempotente_TC_HU_068_08()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        var name = "s057_purga_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        using var client = new MongoClient(documentDb.ConnectionString);
        try
        {
            await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_IDENTITY"] = name });
            var script = $"{MongoToolsContainer.DbDirectory}/057-identity-verificaciones-purga.js";
            var verifications = client.GetDatabase(name).GetCollection<BsonDocument>("verificaciones");
            var purgedAt = new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc);
            await verifications.InsertManyAsync(
            [
                new BsonDocument { ["_id"] = 1, ["usuarioId"] = "ficticio-1", ["estado"] = "en_revision" },
                new BsonDocument { ["_id"] = 2, ["usuarioId"] = "ficticio-2", ["estado"] = "verificada", ["purgadaEn"] = purgedAt, ["razonPurga"] = "decidida" },
            ]);

            (await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script)).ExitCode.Should().Be(0);
            var again = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);

            again.ExitCode.Should().Be(0, again.Output);
            var first = await verifications.Find(Builders<BsonDocument>.Filter.Eq("_id", 1)).FirstAsync();
            first["alertadaEn"].IsBsonNull.Should().BeTrue();
            first["purgadaEn"].IsBsonNull.Should().BeTrue();
            first["razonPurga"].IsBsonNull.Should().BeTrue();
            var second = await verifications.Find(Builders<BsonDocument>.Filter.Eq("_id", 2)).FirstAsync();
            second["purgadaEn"].ToUniversalTime().Should().Be(purgedAt);
            second["razonPurga"].AsString.Should().Be("decidida");
            (await verifications.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(2);
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }
}
