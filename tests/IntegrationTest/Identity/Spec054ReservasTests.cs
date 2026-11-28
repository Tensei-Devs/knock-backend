extern alias IdentityApi;

using System.Security.Cryptography;
using AwesomeAssertions;
using IdentityApi::Knock.Identity.Api.Hosting.Procesos.Outbox;
using IdentityApi::Knock.Identity.Api.Hosting.Procesos.Reintentos;
using IdentityApi::Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Identity;

/// <summary>
/// Spec 054, T-06: las reservas de la bandeja de salida y de las tareas programadas con MongoDB real, dos réplicas sobre la misma base y un reloj simulado (sin esperas reales).
/// Cada réplica es un almacén propio sobre la misma base; un evento o una tarea reservados por una no los reserva la otra.
/// </summary>
public sealed class Spec054ReservasTests
{
    private const int _count = 12;
    private static readonly DateTime _now = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task TwoReplicasOverTheSameBase_ReserveEachEventExactlyOnce_CA_054_16()
    {
        await using var world = await World.StartAsync();
        var ids = await world.SeedEventsAsync(_count);

        var reserved = await Task.WhenAll(Drain(world.Outbox(), _now), Drain(world.Outbox(), _now));

        var all = reserved.SelectMany(list => list).ToList();
        all.Should().HaveCount(_count, "no se pierde ningún evento");
        all.Should().OnlyHaveUniqueItems("ningún evento lo reservan dos réplicas");
        all.Should().BeEquivalentTo(ids);
    }

    [Fact]
    public async Task TwoReplicasOverTheSameBase_ReserveEachTaskExactlyOnce_CA_054_16()
    {
        await using var world = await World.StartAsync();
        var keys = await world.SeedTasksAsync(_count);

        var reserved = await Task.WhenAll(DrainTasks(world.Tasks(), _now), DrainTasks(world.Tasks(), _now));

        var all = reserved.SelectMany(list => list).ToList();
        all.Should().HaveCount(_count);
        all.Should().OnlyHaveUniqueItems();
        all.Should().BeEquivalentTo(keys);
    }

    [Fact]
    public async Task AnEventWhoseLeaseExpired_IsTakenByAnotherReplicaAndTheOldOwnerCannotCloseIt_CA_054_17()
    {
        await using var world = await World.StartAsync();
        await world.SeedEventsAsync(1);
        var crashed = world.Outbox();
        var taker = world.Outbox();
        var first = await crashed.ReserveAsync(_now, CancellationToken.None);

        var whileLeased = await taker.ReserveAsync(_now.AddMinutes(PoliticaDeReintentos.ArrendamientoMinutos - 1), CancellationToken.None);
        var afterTheLease = await taker.ReserveAsync(_now.AddMinutes(PoliticaDeReintentos.ArrendamientoMinutos + 1), CancellationToken.None);

        first.Should().NotBeNull();
        whileLeased.Should().BeNull("mientras dura el arrendamiento nadie más lo toma");
        afterTheLease.Should().NotBeNull();
        afterTheLease.Id.Should().Be(first.Id);
        afterTheLease.ReservaId.Should().NotBe(first.ReservaId);
        (await crashed.MarkDeliveredAsync(first, _now.AddMinutes(4), CancellationToken.None)).Should().BeFalse("la réplica caída ya no es dueña de la reserva");
        (await taker.MarkDeliveredAsync(afterTheLease, _now.AddMinutes(4), CancellationToken.None)).Should().BeTrue();
        (await taker.ReserveAsync(_now.AddMinutes(10), CancellationToken.None)).Should().BeNull("un evento entregado no se vuelve a reservar");
    }

    [Fact]
    public async Task ATaskWhoseLeaseExpired_IsTakenByAnotherReplicaAndFinishedOnce_CA_054_17()
    {
        await using var world = await World.StartAsync();
        await world.SeedTasksAsync(1);
        var crashed = world.Tasks();
        var taker = world.Tasks();
        var first = await crashed.ReserveAsync(_now, [], CancellationToken.None);

        var whileLeased = await taker.ReserveAsync(_now.AddMinutes(PoliticaDeReintentos.ArrendamientoMinutos - 1), [], CancellationToken.None);
        var afterTheLease = await taker.ReserveAsync(_now.AddMinutes(PoliticaDeReintentos.ArrendamientoMinutos + 1), [], CancellationToken.None);

        first.Should().NotBeNull();
        whileLeased.Should().BeNull();
        afterTheLease.Should().NotBeNull();
        afterTheLease.Id.Should().Be(first.Id);
        (await crashed.CompleteAsync(first, _now.AddMinutes(4), CancellationToken.None)).Should().BeFalse();
        (await taker.CompleteAsync(afterTheLease, _now.AddMinutes(4), CancellationToken.None)).Should().BeTrue();
        (await taker.ReserveAsync(_now.AddMinutes(10), [], CancellationToken.None)).Should().BeNull();
    }

    private static async Task<List<string>> Drain(OutboxStore store, DateTime now)
    {
        var taken = new List<string>();
        while (await Retry(() => store.ReserveAsync(now, CancellationToken.None)) is { } item)
        {
            taken.Add(item.Id);
        }

        return taken;
    }

    private static async Task<List<string>> DrainTasks(TareasProgramadasStore store, DateTime now)
    {
        var taken = new List<string>();
        while (await Retry(() => store.ReserveAsync(now, [], CancellationToken.None)) is { } item)
        {
            taken.Add(item.ClaveNegocio);
        }

        return taken;
    }

    /// <summary>Como el ciclo del proceso de fondo: un conflicto de escritura entre réplicas (código 112 o bloqueo mutuo) se vuelve a intentar sin esperar.</summary>
    private static async Task<T> Retry<T>(Func<Task<T>> reserve)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                return await reserve();
            }
            catch (MongoCommandException exception) when (++attempt < 50 && (exception.Code == 112 || exception.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase)))
            {
                await Task.Yield();
            }
        }
    }

    private sealed class World : IAsyncDisposable
    {
        private readonly MongoClient _client;
        private readonly IMongoDatabase _database;

        private World(MongoClient client, IMongoDatabase database)
        {
            _client = client;
            _database = database;
        }

        public static async Task<World> StartAsync()
        {
            var fixture = await DocumentDbFixture.SharedAsync();
            var client = new MongoClient(fixture.ConnectionString);
            return new World(client, client.GetDatabase("s054_res_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8))));
        }

        public OutboxStore Outbox() => new(_database);

        public TareasProgramadasStore Tasks() => new(_database);

        public async Task<List<string>> SeedEventsAsync(int count)
        {
            var ids = new List<string>();
            var store = Outbox();
            using var session = await _client.StartSessionAsync();
            for (var i = 0; i < count; i++)
            {
                var id = OutboxReglas.BuildId("sesion_revocada", Guid.NewGuid().ToString("N"), 1);
                await store.AddAsync(session, new EventoDeOutbox(id, "sesion_revocada", "messaging", new BsonDocument { ["sid"] = Guid.NewGuid().ToString("N") }), _now.AddMinutes(-1), CancellationToken.None);
                ids.Add(id);
            }

            return ids;
        }

        public async Task<List<string>> SeedTasksAsync(int count)
        {
            var keys = new List<string>();
            var store = Tasks();
            for (var i = 0; i < count; i++)
            {
                var key = Guid.NewGuid().ToString("N");
                await store.ScheduleAsync(null, "EliminarCuenta", key, _now.AddMinutes(-1), _now.AddMinutes(-2), CancellationToken.None);
                keys.Add(key);
            }

            return keys;
        }

        public async ValueTask DisposeAsync()
        {
            await _client.DropDatabaseAsync(_database.DatabaseNamespace.DatabaseName);
            _client.Dispose();
        }
    }
}
