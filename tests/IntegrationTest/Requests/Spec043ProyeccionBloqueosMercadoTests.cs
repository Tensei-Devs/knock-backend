extern alias MarketApi;

using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MongoDB.Driver;
using BlockStore = MarketApi::Knock.Market.Api.Modules.Requests.Data.BlockProjectionStore;

namespace Knock.IntegrationTest.Requests;

/// <summary>
/// Spec 043 R-23 y CA-33, T-08: la proyección <c>bloqueos</c> de Mercado queda al día con los eventos de Identidad, un evento repetido o más viejo no la cambia y el bloqueo corta en ambos sentidos
/// (lo que consultan la cotización y el reparto, CA-26 a CA-28). MongoDB real; solo identificadores ficticios.
/// </summary>
public sealed class Spec043ProyeccionBloqueosMercadoTests
{
    private static string Person() => Guid.NewGuid().ToString("D");

    [Fact]
    public async Task TheProjection_FollowsCreateAndDelete_IsIdempotentAndIgnoresOlderEvents_CA_043_33()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        using var client = new MongoClient(fixture.ConnectionString);
        var name = "s043_bloq_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        try
        {
            var store = new BlockStore(client.GetDatabase(name));
            var (blocker, blocked, bystander) = (Person(), Person(), Person());
            var t0 = DateTime.UtcNow;

            await store.ApplyAsync(blocker, blocked, blocked: true, t0, "evento-1", CancellationToken.None);
            await store.ApplyAsync(blocker, blocked, blocked: true, t0, "evento-1", CancellationToken.None);

            (await store.AreBlockedAsync(blocker, blocked, CancellationToken.None)).Should().BeTrue();
            (await store.AreBlockedAsync(blocked, blocker, CancellationToken.None)).Should().BeTrue("el bloqueo corta en ambos sentidos");
            (await store.AreBlockedAsync(blocker, bystander, CancellationToken.None)).Should().BeFalse();
            (await store.BlockedWithAsync(blocker, [blocked, bystander], CancellationToken.None)).Should().ContainSingle().Which.Should().Be(blocked);
            (await client.GetDatabase(name).GetCollection<MongoDB.Bson.BsonDocument>("bloqueos").CountDocumentsAsync(FilterDefinition<MongoDB.Bson.BsonDocument>.Empty)).Should().Be(1, "repetir el evento no duplica");

            await store.ApplyAsync(blocker, blocked, blocked: false, t0.AddSeconds(-30), "evento-viejo", CancellationToken.None);
            (await store.AreBlockedAsync(blocker, blocked, CancellationToken.None)).Should().BeTrue("un evento más viejo no deshace el bloqueo");

            await store.ApplyAsync(blocker, blocked, blocked: false, t0.AddSeconds(30), "evento-2", CancellationToken.None);
            (await store.AreBlockedAsync(blocker, blocked, CancellationToken.None)).Should().BeFalse("desbloquear restablece");
            (await store.BlockedWithAsync(blocker, [blocked], CancellationToken.None)).Should().BeEmpty();
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }
}
