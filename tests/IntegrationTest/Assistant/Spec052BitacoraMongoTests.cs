extern alias MarketApi;

using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Modules.Assistant.Data;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Assistant;

/// <summary>Spec 052, CA-39 contra MongoDB real (documentdb-local): el índice único y el turno por clave impiden duplicar la bitácora aunque el PUT llegue varias veces a la vez.</summary>
public sealed class Spec052BitacoraMongoTests
{
    [Fact]
    public async Task Puts_Repetidos_Y_Simultaneos_No_Dejan_Duplicados_CA_052_39()
    {
        var mongo = await DocumentDbFixture.SharedAsync();
        using var client = new MongoClient(mongo.ConnectionString);
        var database = client.GetDatabase("assistant_it_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6)));
        var collection = database.GetCollection<BsonDocument>(BitacoraStore.CollectionName);
        // Misma definición que db/052-asistente-de-ia.js.
        await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("solicitudId").Ascending("tecnicoId"), new CreateIndexOptions { Unique = true, Name = "solicitudId_1_tecnicoId_1" }));
        var store = new BitacoraStore(database);
        var request = ObjectId.GenerateNewId().ToString();
        var now = DateTime.UtcNow;

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            store.UpsertAsync(new BitacoraRecord(request, "tecnico-1", [$"sabe {i}"], ["b"], ["c"], now, null), CancellationToken.None)));

        results.Count(created => created).Should().Be(1);
        (await collection.CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty)).Should().Be(1);
        (await store.FindAsync(request, "tecnico-1", CancellationToken.None)).Should().NotBeNull();
        (await store.FindAsync(request, "tecnico-2", CancellationToken.None)).Should().BeNull("otro técnico no ve la bitácora");
    }
}
