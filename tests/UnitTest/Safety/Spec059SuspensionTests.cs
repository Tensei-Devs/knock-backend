using AwesomeAssertions;
using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Hosting.Procesos.Outbox;
using Knock.Identity.Api.Modules.Panel;
using Knock.Identity.Api.Modules.Safety.Data;
using Knock.Identity.Api.Modules.Safety.Domain;
using Knock.Identity.Api.Modules.Safety.Services;
using Knock.Identity.Api.Modules.Sesiones;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Knock.UnitTest.Safety;

/// <summary>Spec 059 (T-02 y T-03): reglas puras, configuración y servicio <c>AccountSuspension</c> con dobles en memoria (sin MongoDB ni Redis).</summary>
public sealed class Spec059SuspensionTests
{
    private const string _report = "507f1f77bcf86cd799439011";
    private const string _target = "7b484abb-e9e0-8104-a08d-d30ce15e8148";
    private const string _moderator = "11111111-e9e0-8104-a08d-d30ce15e8148";

    [Theory]
    [InlineData("activa", true, "Suspend")]
    [InlineData("eliminacion_programada", true, "Suspend")]
    [InlineData("suspendida", true, "AlreadySuspended")]
    [InlineData("activa", false, "NotTechnician")]
    public void Suspender_Decide_Por_Estado_Y_Rol_CA_059_04_CA_059_06(string state, bool technician, string expected) =>
        AccountSuspensionRules.Suspender(state, technician, _target, _moderator).ToString().Should().Be(expected);

    [Fact]
    public void Suspender_La_Propia_Cuenta_Se_Rechaza_CA_059_11() =>
        AccountSuspensionRules.Suspender("activa", true, _moderator, _moderator.ToUpperInvariant()).Should().Be(SuspendDecision.SelfSuspension);

    [Fact]
    public void Reactivar_No_Suspendida_Se_Rechaza_CA_059_15()
    {
        AccountSuspensionRules.Reactivar("activa").Should().Be(ReactivateDecision.NotSuspended);
        AccountSuspensionRules.Reactivar("suspendida").Should().Be(ReactivateDecision.Reactivate);
    }

    [Theory]
    [InlineData(2, 3, false)]
    [InlineData(3, 3, true)]
    [InlineData(4, 3, true)]
    [InlineData(50, null, false)]
    public void Umbral_De_Advertencias_Vigentes_CA_059_13(int warnings, int? threshold, bool expected) =>
        AccountSuspensionRules.ShouldApplyGradual(warnings, threshold).Should().Be(expected);

    [Theory]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(0, false)]
    [InlineData(-2, false)]
    public void Validacion_De_Arranque_Rechaza_Umbral_Menor_Que_1_CA_059_13(int? threshold, bool valid) =>
        new SuspensionOptionsValidator().Validate(null, new SuspensionOptions { UmbralAdvertencias = threshold }).Succeeded.Should().Be(valid);

    [Fact]
    public async Task Suspender_Revoca_Y_Deja_Eventos_Auditoria_Y_Cancela_En_Redis_CA_059_01()
    {
        var world = new World();
        world.Accounts.Account = new AccountForSuspension("activa", true);
        world.Accounts.Sessions = ["s1", "s2", "s3"];

        var result = await world.Service.SuspenderAsync(_target, _report, _moderator, SuspensionTypes.Preventive, CancellationToken.None);

        result.Outcome.Should().Be(SuspensionOutcome.Suspended);
        result.SessionIds.Should().Equal("s1", "s2", "s3");
        world.Accounts.SuspendedWith.Should().Be((SuspensionTypes.Preventive, _report));
        world.Panel.PanelRevoked.Should().ContainSingle().Which.Should().Be(_target);
        world.Store.Events.Count(e => e.Tipo == "CuentaSuspendida").Should().Be(1);
        world.Store.Events.Count(e => e.Tipo == "sesion_revocada").Should().Be(3);
        world.Panel.Records.Should().ContainSingle().Which.Should().Be(("cuenta_suspendida", _moderator, _report));
        world.Accounts.Cancelled.Should().Equal("s1", "s2", "s3");
        world.Store.Events.Should().OnlyContain(e => PayloadHasOnlyIdentifiers(e));
    }

    [Fact]
    public async Task Cuenta_De_Cliente_No_Se_Suspende_CA_059_04()
    {
        var world = new World();
        world.Accounts.Account = new AccountForSuspension("activa", false);

        var result = await world.Service.SuspenderAsync(_target, _report, _moderator, SuspensionTypes.Preventive, CancellationToken.None);

        result.Outcome.Should().Be(SuspensionOutcome.NotTechnician);
        world.Accounts.SuspendedWith.Should().BeNull();
        world.Store.Events.Should().BeEmpty();
        world.Panel.Records.Should().BeEmpty();
        world.Accounts.Cancelled.Should().BeEmpty();
    }

    [Fact]
    public async Task Repetida_Sobre_Suspendida_No_Duplica_Nada_CA_059_06()
    {
        var world = new World();
        world.Accounts.Account = new AccountForSuspension("suspendida", true);

        var result = await world.Service.SuspenderAsync(_target, _report, _moderator, SuspensionTypes.Preventive, CancellationToken.None);

        result.Outcome.Should().Be(SuspensionOutcome.AlreadySuspended);
        world.Store.Events.Should().BeEmpty();
        world.Panel.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task Perder_La_Carrera_Contra_Otra_Decision_No_Duplica_CA_059_06()
    {
        var world = new World();
        world.Accounts.Account = new AccountForSuspension("activa", true);
        world.Accounts.LoseRace = true;

        var result = await world.Service.SuspenderAsync(_target, _report, _moderator, SuspensionTypes.Preventive, CancellationToken.None);

        result.Outcome.Should().Be(SuspensionOutcome.AlreadySuspended);
        world.Store.Events.Should().BeEmpty();
        world.Panel.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task Moderador_No_Se_Suspende_A_Si_Mismo_CA_059_11()
    {
        var world = new World();
        world.Accounts.Account = new AccountForSuspension("activa", true);

        var act = async () => await world.Service.SuspenderAsync(_moderator, _report, _moderator, SuspensionTypes.Preventive, CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be(SafetyErrorCodes.CannotSuspendSelfCode);
        world.Accounts.SuspendedWith.Should().BeNull();
        world.Store.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Falla_De_Auditoria_Revierte_Todo_Y_No_Cancela_CA_059_05()
    {
        var world = new World();
        world.Accounts.Account = new AccountForSuspension("activa", true);
        world.Accounts.Sessions = ["s1"];
        world.Panel.FailAudit = true;

        var act = async () => await world.Service.SuspenderAsync(_target, _report, _moderator, SuspensionTypes.Preventive, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        world.Store.Committed.Should().BeFalse("la transacción se descarta completa");
        world.Accounts.Cancelled.Should().BeEmpty("sin commit no se cancela nada en Redis");
    }

    [Fact]
    public async Task Redis_Caido_No_Revierte_Y_Reintenta_CA_059_18()
    {
        var world = new World();
        world.Accounts.Account = new AccountForSuspension("activa", true);
        world.Accounts.Sessions = ["s1"];
        world.Accounts.CancelFailures = 2;

        var result = await world.Service.SuspenderAsync(_target, _report, _moderator, SuspensionTypes.Preventive, CancellationToken.None);

        result.Outcome.Should().Be(SuspensionOutcome.Suspended);
        world.Store.Committed.Should().BeTrue();
        world.Accounts.CancelAttempts.Should().Be(3);
        world.Accounts.Cancelled.Should().Equal("s1");
    }

    [Fact]
    public async Task Redis_Que_Sigue_Caido_Deja_La_Suspension_Y_No_Lanza_CA_059_18()
    {
        var world = new World();
        world.Accounts.Account = new AccountForSuspension("activa", true);
        world.Accounts.Sessions = ["s1"];
        world.Accounts.CancelFailures = 99;

        var result = await world.Service.SuspenderAsync(_target, _report, _moderator, SuspensionTypes.Preventive, CancellationToken.None);

        result.Outcome.Should().Be(SuspensionOutcome.Suspended);
        world.Store.Committed.Should().BeTrue();
        world.Accounts.CancelAttempts.Should().Be(3);
    }

    [Fact]
    public async Task Reactivar_Emite_Evento_Audita_Y_No_Restaura_Sesiones_CA_059_14()
    {
        var world = new World();
        world.Accounts.Account = new AccountForSuspension("suspendida", true);

        await world.Service.ReactivarAsync(_target, _report, _moderator, CancellationToken.None);

        world.Accounts.Reactivated.Should().BeTrue();
        world.Store.Events.Should().ContainSingle().Which.Tipo.Should().Be("CuentaReactivada");
        world.Panel.Records.Should().ContainSingle().Which.Should().Be(("cuenta_reactivada", _moderator, _report));
        world.Accounts.Cancelled.Should().BeEmpty();
    }

    [Fact]
    public async Task Reactivar_Una_Cuenta_No_Suspendida_Se_Rechaza_Sin_Cambios_CA_059_15()
    {
        var world = new World();
        world.Accounts.Account = new AccountForSuspension("activa", true);

        var act = async () => await world.Service.ReactivarAsync(_target, _report, _moderator, CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be(SafetyErrorCodes.AccountNotSuspendedCode);
        world.Accounts.Reactivated.Should().BeFalse();
        world.Store.Events.Should().BeEmpty();
        world.Panel.Records.Should().BeEmpty();
    }

    private static bool PayloadHasOnlyIdentifiers(EventoDeOutbox evento)
    {
        OutboxReglas.Validate(evento);
        return true;
    }

    private sealed class World
    {
        public World()
        {
            Accounts = new FakeAccounts();
            Panel = new FakePanel();
            Store = new FakeStore(Panel);
            Service = new AccountSuspension(Store, Accounts, Panel, new FakeClock(), NullLogger<AccountSuspension>.Instance);
        }

        public FakeAccounts Accounts { get; }

        public FakePanel Panel { get; }

        public FakeStore Store { get; }

        public AccountSuspension Service { get; }
    }

    private sealed class FakeClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

        // Los reintentos de Redis no esperan en las pruebas.
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => base.CreateTimer(callback, state, TimeSpan.Zero, period);
    }

    /// <summary>Transacción simulada: si el trabajo lanza, descarta los eventos y los registros como haría MongoDB.</summary>
    private sealed class FakeStore(FakePanel panel) : ISuspensionStore
    {
        private readonly List<EventoDeOutbox> _events = [];

        public IReadOnlyList<EventoDeOutbox> Events => _events;

        public bool Committed { get; private set; }

        public async Task<T> RunAsync<T>(Func<StoreTransaction, Task<T>> work, CancellationToken cancellationToken)
        {
            try
            {
                var result = await work(new StoreTransaction(this));
                Committed = true;
                return result;
            }
            catch
            {
                _events.Clear();
                panel.Records.Clear();
                throw;
            }
        }

        public Task AddEventsAsync(StoreTransaction transaction, IReadOnlyList<EventoDeOutbox> events, DateTime now, CancellationToken cancellationToken)
        {
            _events.AddRange(events);
            return Task.CompletedTask;
        }
    }

    private sealed class FakePanel : IPanelApi
    {
        public List<(string Event, string Subject, string Resource)> Records { get; } = [];

        public List<string> PanelRevoked { get; } = [];

        public bool FailAudit { get; set; }

        public Task RecordActionAsync(string eventName, string result, string? subject, string? sessionId, string? permission, string? resourceId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RecordActionInTransactionAsync(StoreTransaction transaction, string eventName, string result, string? subject, string? resourceId, CancellationToken cancellationToken)
        {
            if (FailAudit)
            {
                throw new DomainException(DomainErrorKind.ServiceUnavailable, "The audit is not available.", "audit_unavailable");
            }

            Records.Add((eventName, subject ?? string.Empty, resourceId ?? string.Empty));
            return Task.CompletedTask;
        }

        public Task RevokeSessionsInTransactionAsync(StoreTransaction transaction, string subject, CancellationToken cancellationToken)
        {
            PanelRevoked.Add(subject);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAccounts : ISesionesApi
    {
        public AccountForSuspension? Account { get; set; }

        public IReadOnlyList<string> Sessions { get; set; } = [];

        public bool LoseRace { get; set; }

        public (string Type, string Report)? SuspendedWith { get; private set; }

        public bool Reactivated { get; private set; }

        public int CancelFailures { get; set; }

        public int CancelAttempts { get; private set; }

        public List<string> Cancelled { get; } = [];

        public Task<AppAccount?> FindAccountBySubAsync(string subject, CancellationToken cancellationToken) => Task.FromResult<AppAccount?>(null);

        public Task<string?> FindSubByPhoneAsync(string phone, CancellationToken cancellationToken) => Task.FromResult<string?>(null);

        public Task<AccountForSuspension?> ReadAccountForSuspensionAsync(StoreTransaction transaction, string subject, CancellationToken cancellationToken) => Task.FromResult(Account);

        public Task<IReadOnlyList<string>?> SuspendAccountAsync(StoreTransaction transaction, string subject, string expectedState, string type, string reportId, DateTime since, CancellationToken cancellationToken)
        {
            if (LoseRace)
            {
                return Task.FromResult<IReadOnlyList<string>?>(null);
            }

            SuspendedWith = (type, reportId);
            return Task.FromResult<IReadOnlyList<string>?>(Sessions);
        }

        public Task<bool> ReactivateAccountAsync(StoreTransaction transaction, string subject, CancellationToken cancellationToken)
        {
            Reactivated = true;
            return Task.FromResult(true);
        }

        public Task CancelSessionsAsync(IReadOnlyList<string> sessionIds, CancellationToken cancellationToken)
        {
            CancelAttempts++;
            if (CancelAttempts <= CancelFailures)
            {
                throw new InvalidOperationException("redis down");
            }

            Cancelled.AddRange(sessionIds);
            return Task.CompletedTask;
        }
    }
}
