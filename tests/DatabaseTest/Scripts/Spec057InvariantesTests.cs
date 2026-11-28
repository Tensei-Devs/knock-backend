using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.DatabaseTest.Support;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.DatabaseTest.Scripts;

/// <summary>Spec 057, T1 (§5): <c>db/057-identity-verificaciones-purga.js</c> agrega los campos de la purga de forma idempotente y sin cambiar otros campos.</summary>
public sealed class Spec057InvariantesTests
{
    [Fact]
    public async Task Script_Agrega_Campos_De_Purga_Idempotente_Sin_Tocar_Otros_Campos_TC_HU_068_08()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        var name = "s057_identity_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        using var client = new MongoClient(documentDb.ConnectionString);
        try
        {
            await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_IDENTITY"] = name });
            var script = $"{MongoToolsContainer.DbDirectory}/057-identity-verificaciones-purga.js";
            var verifications = client.GetDatabase(name).GetCollection<BsonDocument>("verificaciones");
            var purged = DateTime.UtcNow.AddMinutes(-5);
            await verifications.InsertManyAsync(
            [
                new BsonDocument { ["estado"] = "en_revision", ["usuarioId"] = "ficticio-1" },
                new BsonDocument { ["estado"] = "verificada", ["usuarioId"] = "ficticio-2", ["purgadaEn"] = purged, ["razonPurga"] = "decidida" },
            ]);

            var first = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);
            var second = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", script);

            first.ExitCode.Should().Be(0, first.Output);
            second.ExitCode.Should().Be(0, $"reaplicar el script no falla: {second.Output}");
            var open = await verifications.Find(Builders<BsonDocument>.Filter.Eq("usuarioId", "ficticio-1")).SingleAsync();
            open["alertadaEn"].IsBsonNull.Should().BeTrue();
            open["purgadaEn"].IsBsonNull.Should().BeTrue();
            open["razonPurga"].IsBsonNull.Should().BeTrue();
            open["estado"].AsString.Should().Be("en_revision");
            var decided = await verifications.Find(Builders<BsonDocument>.Filter.Eq("usuarioId", "ficticio-2")).SingleAsync();
            decided["razonPurga"].AsString.Should().Be("decidida", "el script no pisa lo que la decisión ya escribió");
            decided["purgadaEn"].IsBsonNull.Should().BeFalse();
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }
}
