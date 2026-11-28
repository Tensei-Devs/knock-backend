extern alias MarketApi;

using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Modules.Assistant.Data;
using MarketApi::Knock.Market.Api.Modules.Assistant.Services;
using MarketApi::Knock.Market.Api.Modules.Requests;
using MarketApi::Knock.Market.Api.Modules.Requests.Data;
using MarketApi::Knock.Market.Api.Modules.Requests.Services;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>R-21: al cancelar, el envío queda <c>cerrado</c> (el técnico ya no participa), así que la fecha de borrado la pone la revisión periódica.</summary>
    [Fact]
    public async Task Guardada_Antes_De_Cancelar_Queda_Con_Fecha_De_Borrado_CA_052_34()
    {
        var mongo = await DocumentDbFixture.SharedAsync();
        using var client = new MongoClient(mongo.ConnectionString);
        var databaseName = "assistant_it_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        var database = client.GetDatabase(databaseName);
        var store = new BitacoraStore(database);
        var cancelled = ObjectId.GenerateNewId();
        var open = ObjectId.GenerateNewId();
        var savedAt = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
        var cancelledAt = savedAt.AddDays(1);
        await store.UpsertAsync(new BitacoraRecord(cancelled.ToString(), "tecnico-1", ["a"], ["b"], ["c"], savedAt, null), CancellationToken.None);
        await store.UpsertAsync(new BitacoraRecord(open.ToString(), "tecnico-1", ["a"], ["b"], ["c"], savedAt, null), CancellationToken.None);
        await database.GetCollection<BsonDocument>("solicitudes").InsertManyAsync(
        [
            new BsonDocument { { "_id", cancelled }, { "estado", "cancelada" }, { "canceladaEn", cancelledAt } },
            new BsonDocument { { "_id", open }, { "estado", "cotizando" }, { "actualizadoEn", savedAt } },
        ]);
        await using var services = new ServiceCollection()
            .AddScoped<IRequestsApi>(_ => new RequestsApi(new TechnicianAccessStore(database), null!))
            .BuildServiceProvider();

        await new FechadoDeBitacoras(new AsistenteSolicitudesReal(services.GetRequiredService<IServiceScopeFactory>()), store).RunAsync(CancellationToken.None);

        (await store.FindAsync(cancelled.ToString(), "tecnico-1", CancellationToken.None))!.VenceEn.Should().Be(cancelledAt.AddDays(30));
        (await store.FindAsync(open.ToString(), "tecnico-1", CancellationToken.None))!.VenceEn.Should().BeNull();
        await client.DropDatabaseAsync(databaseName);
    }
}
