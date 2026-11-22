extern alias messaging;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;
using AvisoEvent = messaging::Knock.Messaging.Api.Modules.Avisos.Push.AvisoEvent;
using AvisoRules = messaging::Knock.Messaging.Api.Modules.Avisos.Domain.AvisoRules;
using DespachadorDeAvisos = messaging::Knock.Messaging.Api.Modules.Avisos.Push.DespachadorDeAvisos;
using DispatchOutcome = messaging::Knock.Messaging.Api.Modules.Avisos.Push.DispatchOutcome;
using IDeviceTokens = messaging::Knock.Messaging.Api.Modules.Avisos.Push.IDeviceTokens;
using IEnviadosStore = messaging::Knock.Messaging.Api.Modules.Avisos.Push.IEnviadosStore;
using IPresenceReader = messaging::Knock.Messaging.Api.Modules.Avisos.Push.IPresenceReader;
using IPushSender = messaging::Knock.Messaging.Api.Modules.Avisos.Push.IPushSender;
using NoOpPushSender = messaging::Knock.Messaging.Api.Modules.Avisos.Push.NoOpPushSender;
using PushOptions = messaging::Knock.Messaging.Api.Modules.Avisos.Push.PushOptions;
using PushResult = messaging::Knock.Messaging.Api.Modules.Avisos.Push.PushResult;
using WindowDecision = messaging::Knock.Messaging.Api.Modules.Avisos.Domain.WindowDecision;

namespace Knock.UnitTest.Messaging;

public sealed class Spec037DespachoTests
{
    private const string Resource = "507f1f77bcf86cd799439011";
    private const string Conversation = "507f1f77bcf86cd799439012";
    private const string Person = "sub-1";

    [Theory]
    [InlineData("solicitud_nueva", "cotizaciones")]
    [InlineData("cotizacion_aceptada", "visitas")]
    [InlineData("mensaje_nuevo", "mensajes")]
    public void Payload_OnlyV1Fields_CA_037_04(string evento, string tipo)
    {
        var payload = AvisoRules.BuildPayload(evento, Resource, evento == "mensaje_nuevo" ? Conversation : null, Guid.NewGuid());

        payload.Should().NotBeNull();
        payload["tipo"].Should().Be(tipo);
        payload["v"].Should().Be("1");
        payload.Keys.Should().BeSubsetOf(AvisoRules.PayloadFields);
        Guid.TryParse(payload["aviso"], out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("desconocido", Resource)]
    [InlineData("solicitud_nueva", "no-es-objectid")]
    [InlineData("solicitud_nueva", null)]
    [InlineData("mensaje_nuevo", Resource)]
    public void Payload_InvalidEventOrResource_IsRejected_CA_037_04(string evento, string? recurso)
    {
        AvisoRules.BuildPayload(evento, recurso, null, Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void DeduplicationKey_SameEvent_SameKey_DifferentEvents_Differ_CA_037_03()
    {
        AvisoRules.DeduplicationKey("e1", Person).Should().Be(AvisoRules.DeduplicationKey("e1", Person));
        AvisoRules.DeduplicationKey("e1", Person).Should().NotBe(AvisoRules.DeduplicationKey("e2", Person));
        AvisoRules.DeduplicationKey("e1", Person).Should().NotBe(AvisoRules.DeduplicationKey("e1", "sub-2"));
    }

    [Fact]
    public void Window_FirstSends_LaterWithinFiveMinutesGroups_AfterwardsSends_CA_037_05()
    {
        var start = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

        AvisoRules.DecideWindow(null, start).Should().Be(WindowDecision.Send);
        AvisoRules.DecideWindow(start, start.AddMinutes(4).AddSeconds(59)).Should().Be(WindowDecision.Suppress);
        AvisoRules.DecideWindow(start, start.AddMinutes(5)).Should().Be(WindowDecision.Send);
        AvisoRules.ShouldFlushWindow(start, 2, start.AddMinutes(5)).Should().BeTrue();
        AvisoRules.ShouldFlushWindow(start, 0, start.AddMinutes(5)).Should().BeFalse();
        AvisoRules.ShouldFlushWindow(start, 2, start.AddMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public async Task Options_AreOffByDefault_AndNoOpSenderNeverDelivers_R_10()
    {
        PushOptions.Read(new ConfigurationBuilder().Build()).Enabled.Should().BeFalse();
        PushOptions.Read(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [PushOptions.EnabledKey] = "true" }).Build()).Enabled.Should().BeTrue();
        (await new NoOpPushSender().SendAsync("t", new Dictionary<string, string>(), CancellationToken.None)).Should().Be(PushResult.Failed);
    }

    [Fact]
    public async Task Dispatch_SwitchOff_DoesNothing_R_10()
    {
        var harness = new Harness(enabled: false);

        (await harness.Dispatcher.DispatchAsync(Harness.Event("solicitud_nueva"), CancellationToken.None)).Should().Be(DispatchOutcome.Disabled);

        harness.Sender.Calls.Should().Be(0);
        harness.Store.Reserved.Should().BeEmpty();
    }

    [Fact]
    public async Task Dispatch_DuplicateEvent_SendsOnce_CA_037_03()
    {
        var harness = new Harness();

        (await harness.Dispatcher.DispatchAsync(Harness.Event("solicitud_nueva"), CancellationToken.None)).Should().Be(DispatchOutcome.Sent);
        (await harness.Dispatcher.DispatchAsync(Harness.Event("solicitud_nueva"), CancellationToken.None)).Should().Be(DispatchOutcome.Duplicate);

        harness.Sender.Calls.Should().Be(1);
        harness.Sender.LastData!.Keys.Should().BeSubsetOf(AvisoRules.PayloadFields);
    }

    [Fact]
    public async Task Dispatch_Presence_SuppressesNewMessage_CA_037_06()
    {
        var harness = new Harness { Present = true };

        (await harness.Dispatcher.DispatchAsync(Harness.Message("m1"), CancellationToken.None)).Should().Be(DispatchOutcome.Present);

        harness.Sender.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Dispatch_NewMessageWithinWindow_IsGrouped_CA_037_05()
    {
        var harness = new Harness();
        harness.Store.WindowStart = harness.Clock.GetUtcNow().UtcDateTime.AddMinutes(-1);

        (await harness.Dispatcher.DispatchAsync(Harness.Message("m2"), CancellationToken.None)).Should().Be(DispatchOutcome.Grouped);

        harness.Sender.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Dispatch_NewMessageWithinWindow_CountsTheSuppressedMessage_R_07()
    {
        var harness = new Harness();
        harness.Store.WindowStart = harness.Clock.GetUtcNow().UtcDateTime.AddMinutes(-1);

        await harness.Dispatcher.DispatchAsync(Harness.Message("m3"), CancellationToken.None);

        harness.Store.Suppressed.Should().Be(1);
        harness.Store.Opened.Should().Be(0);
    }

    [Fact]
    public async Task Dispatch_FirstNewMessage_OpensTheWindow_R_07()
    {
        var harness = new Harness();

        (await harness.Dispatcher.DispatchAsync(Harness.Message("m4"), CancellationToken.None)).Should().Be(DispatchOutcome.Sent);

        harness.Store.Opened.Should().Be(1);
    }

    [Fact]
    public async Task Dispatch_OverThePersonHourlyLimit_IsNotSentAndIsRecordedAsLimited_CA_037_17()
    {
        var harness = new Harness();
        harness.Store.Recent = PushOptions.DefaultHourlyLimit + 1;

        (await harness.Dispatcher.DispatchAsync(Harness.Event("solicitud_nueva"), CancellationToken.None)).Should().Be(DispatchOutcome.Limited);

        harness.Sender.Calls.Should().Be(0);
        harness.Store.Results.Values.Should().ContainSingle().Which.Should().Be("limitado");
    }

    [Fact]
    public async Task Dispatch_InvalidToken_IsDeleted_CA_037_08()
    {
        var harness = new Harness();
        harness.Sender.Results.Enqueue(PushResult.InvalidToken);

        (await harness.Dispatcher.DispatchAsync(Harness.Event("solicitud_nueva"), CancellationToken.None)).Should().Be(DispatchOutcome.Failed);

        harness.Devices.Deleted.Should().ContainSingle().Which.Should().Be("device-token");
        harness.Sender.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Dispatch_FcmFailure_RetriesThreeTimesAndRecordsFailure_R_09()
    {
        var harness = new Harness();
        for (var i = 0; i < 3; i++)
        {
            harness.Sender.Results.Enqueue(PushResult.Failed);
        }

        (await harness.Dispatcher.DispatchAsync(Harness.Event("solicitud_nueva"), CancellationToken.None)).Should().Be(DispatchOutcome.Failed);
        harness.Sender.Calls.Should().Be(DespachadorDeAvisos.MaxAttempts);
        harness.Store.Results.Values.Should().ContainSingle().Which.Should().Be("fallido");
    }

    [Fact]
    public async Task Dispatch_UnknownEvent_IsInvalidAndNotRecorded_CA_037_04()
    {
        var harness = new Harness();

        (await harness.Dispatcher.DispatchAsync(Harness.Event("otro"), CancellationToken.None)).Should().Be(DispatchOutcome.Invalid);

        harness.Store.Reserved.Should().BeEmpty();
    }

    private sealed class Harness
    {
        public Harness(bool enabled = true)
        {
            Dispatcher = new DespachadorDeAvisos(new PushOptions(enabled), Sender, Store, new Presence(this), Devices, Clock);
        }

        public bool Present { get; set; }

        public FixedClock Clock { get; } = new();

        public FakeSender Sender { get; } = new();

        public FakeStore Store { get; } = new();

        public FakeDevices Devices { get; } = new();

        public DespachadorDeAvisos Dispatcher { get; }

        public static AvisoEvent Event(string evento) => new("evt-1", evento, Person, Resource, null);

        public static AvisoEvent Message(string eventoId) => new(eventoId, "mensaje_nuevo", Person, Resource, Conversation);

        private sealed class Presence(Harness owner) : IPresenceReader
        {
            public Task<bool> IsPresentAsync(string person, CancellationToken cancellationToken) => Task.FromResult(owner.Present);
        }
    }

    /// <summary>Reloj fijo cuyas esperas se cumplen de inmediato.</summary>
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            base.CreateTimer(callback, state, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
    }

    private sealed class FakeSender : IPushSender
    {
        public int Calls { get; private set; }

        public Queue<PushResult> Results { get; } = new();

        public IReadOnlyDictionary<string, string>? LastData { get; private set; }

        public Task<PushResult> SendAsync(string deviceToken, IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken)
        {
            Calls++;
            LastData = data;
            return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : PushResult.Sent);
        }
    }

    private sealed class FakeStore : IEnviadosStore
    {
        public HashSet<string> Reserved { get; } = [];

        public Dictionary<string, string> Results { get; } = [];

        public DateTime? WindowStart { get; set; }

        public int Recent { get; set; }

        public int Opened { get; private set; }

        public int Suppressed { get; private set; }

        public Task OpenWindowAsync(string conversation, string person, DateTime nowUtc, CancellationToken cancellationToken)
        {
            Opened++;
            return Task.CompletedTask;
        }

        public Task CountSuppressedAsync(string conversation, string person, CancellationToken cancellationToken)
        {
            Suppressed++;
            return Task.CompletedTask;
        }

        public Task<int> CountRecentAsync(string person, DateTime sinceUtc, CancellationToken cancellationToken) => Task.FromResult(Recent);

        public Task<bool> TryReserveAsync(string deduplicationKey, string evento, string tipo, CancellationToken cancellationToken) =>
            Task.FromResult(Reserved.Add(deduplicationKey));

        public Task RecordResultAsync(string deduplicationKey, string result, CancellationToken cancellationToken)
        {
            Results[deduplicationKey] = result;
            return Task.CompletedTask;
        }

        public Task<DateTime?> GetWindowStartAsync(string conversation, string person, CancellationToken cancellationToken) =>
            Task.FromResult(WindowStart);
    }

    private sealed class FakeDevices : IDeviceTokens
    {
        public List<string> Deleted { get; } = [];

        public List<string> DeletedSessions { get; } = [];

        public Task DeleteBySessionAsync(string session, CancellationToken cancellationToken)
        {
            DeletedSessions.Add(session);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListAsync(string person, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(["device-token"]);

        public Task DeleteAsync(string person, string deviceToken, CancellationToken cancellationToken)
        {
            Deleted.Add(deviceToken);
            return Task.CompletedTask;
        }
    }
}
