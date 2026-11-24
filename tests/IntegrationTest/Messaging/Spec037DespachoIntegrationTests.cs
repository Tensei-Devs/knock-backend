extern alias MessagingApi;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;
using AvisoEvent = MessagingApi::Knock.Messaging.Api.Modules.Avisos.Push.AvisoEvent;
using DespachadorDeAvisos = MessagingApi::Knock.Messaging.Api.Modules.Avisos.Push.DespachadorDeAvisos;
using DeviceKeyConfiguration = MessagingApi::Knock.Messaging.Api.Modules.Avisos.Services.DeviceKeyConfiguration;
using DeviceStore = MessagingApi::Knock.Messaging.Api.Modules.Avisos.Data.DeviceStore;
using DeviceTokens = MessagingApi::Knock.Messaging.Api.Modules.Avisos.Data.DeviceTokens;
using DispatchOutcome = MessagingApi::Knock.Messaging.Api.Modules.Avisos.Push.DispatchOutcome;
using EnviadosStore = MessagingApi::Knock.Messaging.Api.Modules.Avisos.Data.EnviadosStore;
using IPushSender = MessagingApi::Knock.Messaging.Api.Modules.Avisos.Push.IPushSender;
using PushOptions = MessagingApi::Knock.Messaging.Api.Modules.Avisos.Push.PushOptions;
using PushResult = MessagingApi::Knock.Messaging.Api.Modules.Avisos.Push.PushResult;
using RedisPresenceReader = MessagingApi::Knock.Messaging.Api.Modules.Avisos.Data.RedisPresenceReader;
using TokenProtection = MessagingApi::Knock.Messaging.Api.Modules.Avisos.Services.TokenProtection;

namespace Knock.IntegrationTest.Messaging;

/// <summary>Spec 037 T-05 y T-07: despachador con MongoDB y Redis reales y FCM simulado (CA-03, CA-04, CA-05, CA-06, CA-07, CA-08, CA-12 y carga acotada).</summary>
public sealed class Spec037DespachoIntegrationTests
{
    [Fact]
    public async Task Dispatch_DeduplicatesTheSameEventUnderConcurrency_CA_037_03()
    {
        await using var world = await World.StartAsync();
        var person = World.NewPerson();
        await world.RegisterDeviceAsync(person);
        var evento = World.Event("solicitud_nueva", person);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => world.Dispatcher.DispatchAsync(evento, CancellationToken.None)));

        outcomes.Count(outcome => outcome == DispatchOutcome.Sent).Should().Be(1);
        outcomes.Count(outcome => outcome == DispatchOutcome.Duplicate).Should().Be(7);
        world.Sender.Calls.Should().ContainSingle();
        var stored = await world.Sent.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        stored["resultado"].AsString.Should().Be("enviado");
        stored.ToJson().Should().NotContain(evento.Recurso!, "el registro de envíos no guarda identificadores de recurso (R-06)");
    }

    [Fact]
    public async Task Dispatch_SendsOnlyClosedDataPayloadToTheDecryptedToken_CA_037_04()
    {
        await using var world = await World.StartAsync();
        var person = World.NewPerson();
        var token = await world.RegisterDeviceAsync(person);
        var evento = World.Event("cotizacion_nueva", person);

        var outcome = await world.Dispatcher.DispatchAsync(evento, CancellationToken.None);

        outcome.Should().Be(DispatchOutcome.Sent);
        var call = world.Sender.Calls.Single();
        call.Token.Should().Be(token);
        call.Payload.Keys.Should().BeSubsetOf(["v", "tipo", "evento", "recurso", "aviso", "conversacion"]);
        call.Payload["recurso"].Should().Be(evento.Recurso);
    }

    [Fact]
    public async Task Dispatch_NewMessage_PresenceSkipsAndWindowGroups_CA_037_05_CA_037_06()
    {
        await using var world = await World.StartAsync();
        var present = World.NewPerson();
        var absent = World.NewPerson();
        await world.RegisterDeviceAsync(present);
        await world.RegisterDeviceAsync(absent);
        var conversation = ObjectId.GenerateNewId().ToString();
        await world.Redis.GetDatabase().StringSetAsync(RedisPresenceReader.KeyPrefix + present, "1", TimeSpan.FromSeconds(60));

        var skipped = await world.Dispatcher.DispatchAsync(World.Message(present, conversation), CancellationToken.None);
        var first = await world.Dispatcher.DispatchAsync(World.Message(absent, conversation), CancellationToken.None);
        var second = await world.Dispatcher.DispatchAsync(World.Message(absent, conversation), CancellationToken.None);

        skipped.Should().Be(DispatchOutcome.Present);
        first.Should().Be(DispatchOutcome.Sent);
        second.Should().Be(DispatchOutcome.Grouped);
        world.Sender.Calls.Should().ContainSingle();
        var window = await world.Pending.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        window["suprimidos"].ToInt32().Should().Be(1);
        window["conversacionId"].AsString.Should().Be(conversation);
    }

    [Fact]
    public async Task Dispatch_InvalidTokenIsDeletedAndTransientFailureRetriesThreeTimes_CA_037_07_CA_037_08_CA_037_11()
    {
        await using var world = await World.StartAsync();
        var invalid = World.NewPerson();
        var flaky = World.NewPerson();
        var invalidToken = await world.RegisterDeviceAsync(invalid);
        await world.RegisterDeviceAsync(flaky);
        world.Sender.Respond = token => token == invalidToken ? PushResult.InvalidToken : PushResult.Failed;

        var rejected = await world.Dispatcher.DispatchAsync(World.Event("solicitud_nueva", invalid), CancellationToken.None);
        var failed = await world.Dispatcher.DispatchAsync(World.Event("solicitud_nueva", flaky), CancellationToken.None);

        rejected.Should().Be(DispatchOutcome.Failed);
        (await world.Tokens.ListAsync(invalid, CancellationToken.None)).Should().BeEmpty("FCM invalidó el identificador y se borra");
        failed.Should().Be(DispatchOutcome.Failed);
        world.Sender.Calls.Count(call => call.Token != invalidToken).Should().Be(DespachadorDeAvisos.MaxAttempts);
        (await world.Sent.Find(Builders<BsonDocument>.Filter.Eq("destinatario", flaky)).SingleAsync())["resultado"].AsString.Should().Be("fallido");
    }

    [Fact]
    public async Task Dispatch_UndecryptableDeviceIsSkippedWithoutLeakingValues_CA_037_12()
    {
        await using var world = await World.StartAsync();
        var person = World.NewPerson();
        await world.RegisterDeviceAsync(person);
        var corrupt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(40));
        await world.Devices.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("personaId", person), Builders<BsonDocument>.Update.Set("identificadorCifrado", corrupt));

        var outcome = await world.Dispatcher.DispatchAsync(World.Event("solicitud_nueva", person), CancellationToken.None);

        outcome.Should().Be(DispatchOutcome.NoDevices);
        world.Sender.Calls.Should().BeEmpty();
        world.Logs.AllText().Should().NotContain(corrupt).And.NotContain(person);
    }

    [Fact]
    public async Task Dispatch_ThousandEventsToTwoHundredPeople_NoLossesNoDuplicates_CA_037_03()
    {
        await using var world = await World.StartAsync();
        var people = Enumerable.Range(0, 200).Select(_ => World.NewPerson()).ToList();
        foreach (var person in people)
        {
            await world.RegisterDeviceAsync(person);
        }

        var events = Enumerable.Range(0, 1000).Select(index => World.Event("solicitud_nueva", people[index % people.Count])).ToList();
        var firstRun = await world.DispatchAllAsync(events);
        var replay = await world.DispatchAllAsync(events.Take(100));

        firstRun.Should().OnlyContain(outcome => outcome == DispatchOutcome.Sent);
        replay.Should().OnlyContain(outcome => outcome == DispatchOutcome.Duplicate);
        world.Sender.Calls.Count.Should().Be(1000);
        (await world.Sent.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(1000);
        world.Sender.Calls.Select(call => call.Payload["aviso"]).Distinct().Count().Should().Be(1000);
    }

    [Fact]
    public async Task Dispatch_TechnicianAbsentNotice_ReachesOnlyTheTechnicianOnceEvenIfRepeated_CA_064_08()
    {
        await using var world = await World.StartAsync();
        var technician = World.NewPerson();
        var other = World.NewPerson();
        var technicianToken = await world.RegisterDeviceAsync(technician);
        await world.RegisterDeviceAsync(other);
        var evento = World.Event("tecnico_no_se_presento", technician);

        var first = await world.Dispatcher.DispatchAsync(evento, CancellationToken.None);
        var repeated = await world.Dispatcher.DispatchAsync(evento, CancellationToken.None);

        first.Should().Be(DispatchOutcome.Sent);
        repeated.Should().Be(DispatchOutcome.Duplicate);
        var call = world.Sender.Calls.Single();
        call.Token.Should().Be(technicianToken, "solo el técnico recibe el aviso");
        call.Payload["tipo"].Should().Be("visitas");
        call.Payload["evento"].Should().Be("tecnico_no_se_presento");
        call.Payload.Keys.Should().BeSubsetOf(["v", "tipo", "evento", "recurso", "aviso"]);
    }

    private sealed class FakeSender : IPushSender
    {
        private readonly ConcurrentQueue<(string Token, IReadOnlyDictionary<string, string> Payload)> _calls = new();

        public Func<string, PushResult> Respond { get; set; } = _ => PushResult.Sent;

        public ConcurrentQueue<(string Token, IReadOnlyDictionary<string, string> Payload)> Calls => _calls;

        public Task<PushResult> SendAsync(string deviceToken, IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken)
        {
            _calls.Enqueue((deviceToken, data));
            return Task.FromResult(Respond(deviceToken));
        }
    }

    private sealed class LogSink : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _lines = new();

        public string AllText() => string.Join('\n', _lines);

        public ILogger CreateLogger(string categoryName) => new SinkLogger(_lines);

        public void Dispose()
        {
        }

        private sealed class SinkLogger(ConcurrentQueue<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                lines.Enqueue(formatter(state, exception) + exception);
        }
    }

    private sealed class World : IAsyncDisposable
    {
        private readonly MongoClient _client;
        private readonly string _databaseName;
        private readonly DeviceStore _store;
        private readonly ILoggerFactory _loggers;

        private World(MongoClient client, IMongoDatabase database, ConnectionMultiplexer redis, TokenProtection protection)
        {
            _client = client;
            _databaseName = database.DatabaseNamespace.DatabaseName;
            Redis = redis;
            Devices = database.GetCollection<BsonDocument>("notifications.dispositivos");
            Sent = database.GetCollection<BsonDocument>("notifications.enviados");
            Pending = database.GetCollection<BsonDocument>("notifications.pendientes");
            _store = new DeviceStore(database, protection);
            _loggers = LoggerFactory.Create(builder => builder.AddProvider(Logs));
            Tokens = new DeviceTokens(database, new DeviceKeyConfiguration(protection), _loggers.CreateLogger<DeviceTokens>());
            Dispatcher = new DespachadorDeAvisos(new PushOptions(true, 100), Sender, new EnviadosStore(database), new RedisPresenceReader(redis), Tokens, TimeProvider.System);
        }

        public ConnectionMultiplexer Redis { get; }

        public IMongoCollection<BsonDocument> Devices { get; }

        public IMongoCollection<BsonDocument> Sent { get; }

        public IMongoCollection<BsonDocument> Pending { get; }

        public DeviceTokens Tokens { get; }

        public LogSink Logs { get; } = new();

        public FakeSender Sender { get; } = new();

        public DespachadorDeAvisos Dispatcher { get; }

        public static async Task<World> StartAsync()
        {
            var mongo = await DocumentDbFixture.SharedAsync();
            var redisFixture = await RedisTestFixture.SharedAsync();
            var client = new MongoClient(mongo.ConnectionString);
            var database = client.GetDatabase("s037_disp_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8)));
            await UniqueIndexAsync(database, "notifications.dispositivos", "tokenHuella");
            await UniqueIndexAsync(database, "notifications.dispositivos", "id");
            await UniqueIndexAsync(database, "notifications.enviados", "eventoId", "destinatario");
            await UniqueIndexAsync(database, "notifications.pendientes", "conversacionId", "destinatario");
            var protection = new TokenProtection("1", RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32));
            return new World(client, database, await ConnectionMultiplexer.ConnectAsync(redisFixture.ConnectionString), protection);
        }

        public static string NewPerson() => Guid.NewGuid().ToString("D");

        public static AvisoEvent Event(string tipo, string person) => new(Guid.NewGuid().ToString("N"), tipo, person, ObjectId.GenerateNewId().ToString(), null);

        public static AvisoEvent Message(string person, string conversation) =>
            new(Guid.NewGuid().ToString("N"), "mensaje_nuevo", person, ObjectId.GenerateNewId().ToString(), conversation);

        /// <summary>Entrega los eventos con 32 a la vez, como un trabajador con concurrencia acotada (la cola del controlador de MongoDB es finita).</summary>
        public async Task<DispatchOutcome[]> DispatchAllAsync(IEnumerable<AvisoEvent> events)
        {
            using var gate = new SemaphoreSlim(32);
            return await Task.WhenAll(events.Select(async evento =>
            {
                await gate.WaitAsync();
                try
                {
                    return await Dispatcher.DispatchAsync(evento, CancellationToken.None);
                }
                finally
                {
                    gate.Release();
                }
            }));
        }

        public async Task<string> RegisterDeviceAsync(string person)
        {
            var token = "tk-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            await _store.RegisterAsync(person, "sid-" + person, "android", token, DateTime.UtcNow, CancellationToken.None);
            return token;
        }

        public async ValueTask DisposeAsync()
        {
            _loggers.Dispose();
            await Redis.DisposeAsync();
            await _client.DropDatabaseAsync(_databaseName);
            _client.Dispose();
        }

        private static Task<string> UniqueIndexAsync(IMongoDatabase database, string collection, params string[] fields)
        {
            var keys = Builders<BsonDocument>.IndexKeys;
            var definition = keys.Combine(fields.Select(field => keys.Ascending(field)));
            return database.GetCollection<BsonDocument>(collection).Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(definition, new CreateIndexOptions { Unique = true }));
        }
    }
}
