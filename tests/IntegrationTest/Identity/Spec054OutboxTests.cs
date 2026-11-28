extern alias IdentityApi;

using System.Security.Cryptography;
using AwesomeAssertions;
using IdentityApi::Knock.Identity.Api.Hosting.Procesos.Outbox;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Identity;

/// <summary>Spec 054, R-02 y CA-02: la bandeja de salida de Identidad escribe en la transacción de quien la llama. MongoDB real; solo datos ficticios.</summary>
public sealed class Spec054OutboxTests
{
    [Fact]
    public async Task Evento_En_La_Misma_Transaccion_CA_054_02()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        using var client = new MongoClient(fixture.ConnectionString);
        var name = "s054_tx_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        var database = client.GetDatabase(name);
        try
        {
            await database.CreateCollectionAsync("outbox");
            await database.CreateCollectionAsync("cambios");
            var changes = database.GetCollection<BsonDocument>("cambios");
            var events = database.GetCollection<BsonDocument>("outbox");
            var store = new OutboxStore(database);
            var id = OutboxReglas.BuildId("sesion_revocada", Guid.NewGuid().ToString("N"), 1);
            var evento = new EventoDeOutbox(id, "sesion_revocada", "messaging", new BsonDocument { ["sid"] = Guid.NewGuid().ToString("N") });

            async Task WriteBothAsync(bool commit)
            {
                using var session = await client.StartSessionAsync();
                session.StartTransaction();
                await changes.InsertOneAsync(session, new BsonDocument("_id", evento.Id));
                await store.AddAsync(session, evento, DateTime.UtcNow, CancellationToken.None);
                await (commit ? session.CommitTransactionAsync() : session.AbortTransactionAsync());
            }

            await WriteBothAsync(commit: false);

            (await events.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(0, "si la transacción falla no queda evento");
            (await changes.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(0);

            await WriteBothAsync(commit: true);

            (await events.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(1);
            (await changes.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(1);
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }
}
