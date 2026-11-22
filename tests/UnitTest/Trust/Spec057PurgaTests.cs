using AwesomeAssertions;
using Knock.Identity.Api.Hosting.Procesos.Outbox;
using Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;
using Knock.Identity.Api.Modules.Moderation.Services;
using Knock.Identity.Api.Modules.Panel;
using Knock.Identity.Api.Modules.Trust.Data;
using Knock.Identity.Api.Modules.Trust.Domain;
using Knock.Identity.Api.Modules.Trust.Services;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.UnitTest.Trust;

/// <summary>Spec 057 (T2 y T3): purga idempotente, manejadores de tarea y programación con dobles en memoria y tiempo simulado (sin MongoDB ni Blob).</summary>
public sealed class Spec057PurgaTests
{
    private static readonly DateTime _sent = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Repetir_La_Purga_No_Cambia_Nada_TC_HU_068_06()
    {
        var id = ObjectId.GenerateNewId();
        var store = new FakePurgeStore();
        var audit = new FakePanel();
        var service = Service(store, new FakeBlobs(), audit);

        var first = await service.PurgeAsync(id, PurgeReasons.Expired, PurgeReasons.SystemActor, CancellationToken.None);
        var second = await service.PurgeAsync(id, PurgeReasons.Expired, PurgeReasons.SystemActor, CancellationToken.None);

        first.Should().Be(PurgeOutcome.Purged);
        second.Should().Be(PurgeOutcome.AlreadyPurged);
        audit.Records.Should().ContainSingle().Which.Should().Be((PurgeEvents.AuditPurged, PurgeReasons.SystemActor, id.ToString()));
    }

    [Fact]
    public async Task Fallo_Al_Borrar_Blob_Deja_La_Llave_Destruida_Y_El_Reintento_Termina_El_Borrado_TC_HU_068_10()
    {
        var id = ObjectId.GenerateNewId();
        var store = new FakePurgeStore();
        var blobs = new FakeBlobs { Failures = 1 };
        var service = Service(store, blobs, new FakePanel());

        var failing = async () => await service.PurgeAsync(id, PurgeReasons.Expired, PurgeReasons.SystemActor, CancellationToken.None);
        await failing.Should().ThrowAsync<InvalidOperationException>();
        store.Purged.Should().Contain(id, "la llave ya se destruyó antes de tocar los blobs");

        (await service.PurgeAsync(id, PurgeReasons.Expired, PurgeReasons.SystemActor, CancellationToken.None)).Should().Be(PurgeOutcome.AlreadyPurged);
        blobs.Deleted.Should().ContainSingle().Which.Should().Be(id.ToString());
    }

    [Fact]
    public async Task Ya_No_Esta_En_Revision_No_Purga_Ni_Toca_Blobs_TC_HU_068_07()
    {
        var store = new FakePurgeStore { Eligible = false };
        var blobs = new FakeBlobs();

        var outcome = await Service(store, blobs, new FakePanel()).PurgeAsync(ObjectId.GenerateNewId(), PurgeReasons.Expired, PurgeReasons.SystemActor, CancellationToken.None);

        outcome.Should().Be(PurgeOutcome.NotEligible);
        blobs.Deleted.Should().BeEmpty();
    }

    [Fact]
    public async Task Baja_De_Cuenta_Purga_Cada_Verificacion_De_La_Persona_TC_HU_068_11()
    {
        var first = ObjectId.GenerateNewId();
        var second = ObjectId.GenerateNewId();
        var store = new FakePurgeStore { OwnerVerifications = [first, second] };
        var audit = new FakePanel();

        await Service(store, new FakeBlobs(), audit).PurgeAccountAsync("sub-1", CancellationToken.None);

        store.Purged.Should().BeEquivalentTo([first, second]);
        store.Reasons.Should().OnlyContain(x => x == PurgeReasons.AccountDeletion);
        audit.Records.Should().HaveCount(2);
    }

    [Fact]
    public async Task Alerta_A_Las_24_Horas_Una_Sola_Vez_TC_HU_068_02()
    {
        var id = ObjectId.GenerateNewId();
        var store = new FakePurgeStore();
        var handler = new AvisarAlVerificadorHandler(store, new FixedTime(_sent.AddHours(24)));
        var task = new TareaReservada(ObjectId.GenerateNewId(), PurgeEvents.AlertTask, id.ToString(), "reserva", 0);

        await handler.HandleAsync(task, CancellationToken.None);
        await handler.HandleAsync(task, CancellationToken.None);

        store.Alerted.Should().ContainSingle().Which.Should().Be(id);
    }

    [Fact]
    public async Task Clave_De_Negocio_Invalida_Termina_Sin_Efecto_TC_HU_068_06()
    {
        var store = new FakePurgeStore();
        var task = new TareaReservada(ObjectId.GenerateNewId(), PurgeEvents.AlertTask, "no-es-un-id", "reserva", 0);

        await new AvisarAlVerificadorHandler(store, TimeProvider.System).HandleAsync(task, CancellationToken.None);
        await new BorrarFotosDeVerificacionHandler(Service(store, new FakeBlobs(), new FakePanel())).HandleAsync(task, CancellationToken.None);

        store.Alerted.Should().BeEmpty();
        store.Purged.Should().BeEmpty();
    }

    [Fact]
    public async Task Manejador_De_Siete_Dias_Purga_Con_Razon_Vencida_TC_HU_068_03()
    {
        var id = ObjectId.GenerateNewId();
        var store = new FakePurgeStore();
        var task = new TareaReservada(ObjectId.GenerateNewId(), PurgeEvents.DeleteTask, id.ToString(), "reserva", 0);

        await new BorrarFotosDeVerificacionHandler(Service(store, new FakeBlobs(), new FakePanel())).HandleAsync(task, CancellationToken.None);

        store.Reasons.Should().ContainSingle().Which.Should().Be(PurgeReasons.Expired);
    }

    [Fact]
    public async Task Envio_Programa_Alerta_Y_Borrado_Con_La_Clave_De_La_Verificacion_TC_HU_068_13()
    {
        var id = ObjectId.GenerateNewId();
        var tasks = new FakeTasksStore();

        await new VerificationPurgeScheduler(tasks, new FixedTime(_sent)).ScheduleAsync(null, id, _sent, CancellationToken.None);
        await new VerificationPurgeScheduler(tasks, new FixedTime(_sent)).ScheduleAsync(null, id, _sent, CancellationToken.None);

        tasks.Scheduled.Should().BeEquivalentTo(
            new[]
            {
                (PurgeEvents.AlertTask, id.ToString(), _sent.AddHours(24)),
                (PurgeEvents.DeleteTask, id.ToString(), _sent.AddDays(7)),
            });
    }

    [Fact]
    public void Manejadores_Son_Del_Catalogo_De_La_Spec_054_TC_HU_068_13()
    {
        CatalogoDeTareas.Contiene(PurgeEvents.AlertTask).Should().BeTrue();
        CatalogoDeTareas.Contiene(PurgeEvents.DeleteTask).Should().BeTrue();
        new AvisarAlVerificadorHandler(new FakePurgeStore(), TimeProvider.System).Tipo.Should().Be(PurgeEvents.AlertTask);
    }

    [Fact]
    public void Eventos_De_La_Purga_Estan_En_El_Catalogo_Y_Su_Carga_Solo_Trae_Identificadores_TC_HU_068_14()
    {
        foreach (var type in new[] { PurgeEvents.UnreviewedAlert, PurgeEvents.PhotosDeleted })
        {
            var payload = new BsonDocument { ["verificacionId"] = "1", ["usuarioId"] = "2", ["razon"] = PurgeReasons.Expired, ["enviadoEn"] = "2026-11-21T12:00:00.0000000Z" };
            var act = () => OutboxReglas.Validate(new EventoDeOutbox(OutboxReglas.BuildId(type, "1", 1), type, "messaging", payload));
            act.Should().NotThrow();
        }
    }

    private static PurgaDeVerificacion Service(FakePurgeStore store, FakeBlobs blobs, FakePanel panel) => new(store, blobs, panel, new FixedTime(_sent));

    private sealed class FakePurgeStore : IVerificationPurgeStore
    {
        public bool Eligible { get; init; } = true;

        public List<ObjectId> OwnerVerifications { get; init; } = [];

        public HashSet<ObjectId> Purged { get; } = [];

        public List<string> Reasons { get; } = [];

        public List<ObjectId> Alerted { get; } = [];

        public async Task<PurgeOutcome> PurgeAsync(ObjectId id, string reason, DateTime now, Func<StoreTransaction, Task> writeAudit, CancellationToken cancellationToken)
        {
            if (Purged.Contains(id))
            {
                return PurgeOutcome.AlreadyPurged;
            }

            if (!Eligible)
            {
                return PurgeOutcome.NotEligible;
            }

            Purged.Add(id);
            Reasons.Add(reason);
            await writeAudit(new StoreTransaction(new object()));
            return PurgeOutcome.Purged;
        }

        public Task<bool> MarkAlertedAsync(ObjectId id, DateTime now, CancellationToken cancellationToken)
        {
            if (Alerted.Contains(id))
            {
                return Task.FromResult(false);
            }

            Alerted.Add(id);
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<ObjectId>> FindUnpurgedAsync(string sub, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ObjectId>>(OwnerVerifications);
    }

    private sealed class FakeBlobs : IVerificationBlobPurger
    {
        public int Failures { get; set; }

        public List<string> Deleted { get; } = [];

        public Task PurgeBlobsAsync(string verificationId, CancellationToken cancellationToken)
        {
            if (Failures > 0)
            {
                Failures--;
                throw new InvalidOperationException("blob_unavailable");
            }

            Deleted.Add(verificationId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakePanel : IPanelApi
    {
        public List<(string Event, string Subject, string Resource)> Records { get; } = [];

        public Task RecordActionAsync(string eventName, string result, string? subject, string? sessionId, string? permission, string? resourceId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RecordActionInTransactionAsync(StoreTransaction transaction, string eventName, string result, string? subject, string? resourceId, CancellationToken cancellationToken)
        {
            Records.Add((eventName, subject ?? string.Empty, resourceId ?? string.Empty));
            return Task.CompletedTask;
        }

        public Task RevokeSessionsInTransactionAsync(StoreTransaction transaction, string subject, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeTasksStore : ITareasProgramadasStore
    {
        public HashSet<(string Type, string Key, DateTime Due)> Scheduled { get; } = [];

        public Task<bool> ScheduleAsync(IClientSessionHandle? session, string type, string businessKey, DateTime due, DateTime now, CancellationToken cancellationToken) =>
            Task.FromResult(Scheduled.Add((type, businessKey, due)));

        public Task<TareaReservada?> ReserveAsync(DateTime now, IReadOnlyCollection<string> excludedTypes, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> CompleteAsync(TareaReservada task, DateTime now, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> FailAsync(TareaReservada task, DateTime now, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> AbortAsync(TareaReservada task, DateTime now, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> ConfirmLeaseAsync(IClientSessionHandle session, TareaReservada task, DateTime now, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FixedTime(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }
}
