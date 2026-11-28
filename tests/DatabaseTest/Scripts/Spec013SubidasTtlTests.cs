using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.DatabaseTest.Support;
using Knock.Hosting.Data;
using Knock.IntegrationTest.Support;
using Knock.Market.Api.Modules.Media.Data;
using Knock.Market.Api.Modules.Media.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.DatabaseTest.Scripts;

/// <summary>
/// Spec 013, R-08 y CA-07: con el índice TTL de <c>db/044</c> y <c>SubidasImagenStore</c> reales, a los 7 días vence el registro de una subida pendiente o rechazada y el de una
/// publicada no vence. No se espera al proceso de limpieza de la base: se comprueba qué documentos alcanza el índice (<c>expireAfterSeconds: 0</c> sobre <c>expiraEn</c>).
/// </summary>
public sealed class Spec013SubidasTtlTests
{
    [Fact]
    public async Task OnlyPendingAndRejectedUploadsExpireAfterSevenDays_ThePublishedOneStays_CA_013_07()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        var name = "s013_ttl_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        using var client = new MongoClient(documentDb.ConnectionString);
        try
        {
            await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_MARKET"] = name });
            var applied = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", $"{MongoToolsContainer.DbDirectory}/044-market-subidas-imagen.js");
            applied.ExitCode.Should().Be(0, applied.Output);
            var database = client.GetDatabase(name);
            var store = new SubidasImagenStore(database, Options.Create(new KnockMongoOptions()));
            var now = DateTime.UtcNow;
            foreach (var id in new[] { "pendiente", "rechazada", "invalida", "publicada" })
            {
                await store.InsertAsync(SubidaImagen.Nueva(id, "blob-" + id, "persona-ficticia", PropositoImagen.Trabajo, "image/jpeg", 100, now), CancellationToken.None);
            }

            (await store.MarcarFinalAsync("rechazada", EstadoSubida.Rechazada, CancellationToken.None)).Should().BeTrue();
            (await store.MarcarFinalAsync("invalida", EstadoSubida.Invalida, CancellationToken.None)).Should().BeTrue();
            (await store.MarcarPublicadaAsync("publicada", "direccion-ficticia", 10, 10, 100, CancellationToken.None)).Should().BeTrue();

            (await MongoIndexes.DescribeAsync(database, SubidasImagenStore.Coleccion)).Should().Contain("expiraEn_1 expiraEn:1 - ttl:0");
            (await ExpiredByAsync(database, now.AddDays(7).AddMinutes(-1))).Should().BeEmpty("antes de los 7 días no vence ninguna");
            (await ExpiredByAsync(database, now.AddDays(7).AddMinutes(1))).Should().BeEquivalentTo("pendiente", "rechazada", "invalida");
            (await ExpiredByAsync(database, now.AddYears(100))).Should().NotContain("publicada", "el registro de una publicada se conserva");
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }

    private static async Task<List<string>> ExpiredByAsync(IMongoDatabase database, DateTime instant)
    {
        var expired = await database.GetCollection<BsonDocument>(SubidasImagenStore.Coleccion).Find(Builders<BsonDocument>.Filter.Lte("expiraEn", instant)).ToListAsync();
        return [.. expired.Select(document => document["_id"].AsString)];
    }
}
