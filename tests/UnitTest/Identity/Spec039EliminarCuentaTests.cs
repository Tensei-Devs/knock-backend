using AwesomeAssertions;
using Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;
using Knock.Identity.Api.Modules.Account.Data;
using Knock.Identity.Api.Modules.Trust;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 039, R-25 (T-09): la tarea <c>EliminarCuenta</c> borra al vencer la gracia, llama a la purga de fotos de la spec 057 y es idempotente. Sin MongoDB.</summary>
public class Spec039EliminarCuentaTests
{
    private const string _userId = "665f1b2c3d4e5f6071829304";
    private const string _sub = "0a1b2c3d-0000-8000-8000-000000000001";

    private static readonly DateTime _deleteAt = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

    internal sealed class FakeTrust : ITrustApi
    {
        public List<string> Purged { get; } = [];

        public bool Fail { get; set; }

        public bool IsCatalogReason(string? reason) => false;

        public Task PurgeAccountAsync(string accountSub, CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new InvalidOperationException("blob store down");
            }

            Purged.Add(accountSub);
            return Task.CompletedTask;
        }
    }

    internal sealed record Rig(EliminarCuentaHandler Handler, FakeAccountStore Store, FakeTrust Trust, Spec039Outbox Outbox, Spec039Cancelled Cancelled);

    internal static Rig Build(string state, DateTime? deleteAt, DateTime now)
    {
        var store = new FakeAccountStore();
        store.Accounts.Add(new AccountSnapshot(_userId, _sub, state, false, [1, 2, 3], deleteAt));
        store.Sessions.Add(new FakeAccountSession("665f1b2c3d4e5f6071829a01", _userId));
        store.Sessions.Add(new FakeAccountSession("665f1b2c3d4e5f6071829a02", _userId));
        var trust = new FakeTrust();
        var outbox = new Spec039Outbox();
        var cancelled = new Spec039Cancelled();
        return new Rig(new EliminarCuentaHandler(store, trust, outbox, cancelled, new Spec039Clock(new DateTimeOffset(now)), NullLogger<EliminarCuentaHandler>.Instance), store, trust, outbox, cancelled);
    }

    private static TareaReservada Reserved(DateTime deleteAt) =>
        new(ObjectId.GenerateNewId(), AccountOutbox.DeletionTask, AccountOutbox.DeletionKey(_sub, deleteAt), "r", 0);

    [Fact]
    public async Task WhenTheGraceExpires_PurgesPhotosErasesTheAccountAndClosesSessions_CA_039_25()
    {
        var rig = Build("eliminacion_programada", _deleteAt, _deleteAt.AddMinutes(1));

        await rig.Handler.HandleAsync(Reserved(_deleteAt), CancellationToken.None);

        rig.Trust.Purged.Should().Equal(_sub);
        rig.Outbox.AccountDeleted.Should().Equal(_sub);
        rig.Store.Erased.Should().Equal(_userId);
        rig.Store.Accounts.Single().State.Should().Be("eliminada");
        rig.Cancelled.Cancelled.Select(x => x.SessionId).Should().BeEquivalentTo("665f1b2c3d4e5f6071829a01", "665f1b2c3d4e5f6071829a02");
    }

    [Fact]
    public async Task WhenThePersonCancelled_TheTaskDoesNothing_CA_039_32()
    {
        var rig = Build("activa", null, _deleteAt.AddMinutes(1));

        await rig.Handler.HandleAsync(Reserved(_deleteAt), CancellationToken.None);

        rig.Trust.Purged.Should().BeEmpty();
        rig.Store.Erased.Should().BeEmpty();
        rig.Outbox.AccountDeleted.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenTheGraceIsStillRunning_TheTaskDoesNothing()
    {
        var rig = Build("eliminacion_programada", _deleteAt, _deleteAt.AddMinutes(-1));

        await rig.Handler.HandleAsync(Reserved(_deleteAt), CancellationToken.None);

        rig.Store.Erased.Should().BeEmpty();
    }

    [Fact]
    public async Task ATaskOfAnEarlierGrace_DoesNotDeleteAReScheduledAccount()
    {
        // Canceló y volvió a programar: la gracia vigente es otra y la tarea vieja no borra nada.
        var rig = Build("eliminacion_programada", _deleteAt.AddDays(3), _deleteAt.AddDays(4));

        await rig.Handler.HandleAsync(Reserved(_deleteAt), CancellationToken.None);

        rig.Store.Erased.Should().BeEmpty();
    }

    [Fact]
    public async Task RunningTwice_ErasesOnce_CA_054_14()
    {
        var rig = Build("eliminacion_programada", _deleteAt, _deleteAt.AddMinutes(1));

        await rig.Handler.HandleAsync(Reserved(_deleteAt), CancellationToken.None);
        await rig.Handler.HandleAsync(Reserved(_deleteAt), CancellationToken.None);

        rig.Store.Erased.Should().HaveCount(1);
        rig.Trust.Purged.Should().HaveCount(1);
        rig.Store.Accounts.Single().State.Should().Be("eliminada", "la segunda ejecución deja el mismo resultado");
    }

    [Fact]
    public async Task WhenThePhotoPurgeFails_NothingIsErasedSoTheTaskRetries()
    {
        var rig = Build("eliminacion_programada", _deleteAt, _deleteAt.AddMinutes(1));
        rig.Trust.Fail = true;

        var act = () => rig.Handler.HandleAsync(Reserved(_deleteAt), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        rig.Store.Erased.Should().BeEmpty();
        rig.Store.Accounts.Single().State.Should().Be("eliminacion_programada");
    }

    [Fact]
    public void TheDeletionKeyHoldsOnlyTheSubAndTheInstant()
    {
        AccountOutbox.DeletionKey(_sub, _deleteAt).Should().Be($"{_sub}:{new DateTimeOffset(_deleteAt).ToUnixTimeSeconds()}");
    }
}
