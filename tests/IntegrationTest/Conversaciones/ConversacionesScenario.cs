extern alias MessagingApi;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Knock.Contracts.Conversations;
using Knock.Contracts.Moderation;
using Knock.IntegrationTest.Chat;
using Knock.IntegrationTest.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.IntegrationTest.Conversaciones;

/// <summary>Moderación de mentira (spec 030, R-13): permite, rechaza, no responde o lanza, según <see cref="Outcome"/>. Cuenta las llamadas y guarda el último texto revisado para ver qué recibió.</summary>
internal sealed class FakeModerator : IContentModerator
{
    private int _calls;

    public ModerationOutcome Outcome { get; set; } = ModerationOutcome.Allowed;

    public bool Throws { get; set; }

    public int Calls => Volatile.Read(ref _calls);

    public string? LastText { get; private set; }

    public Task<ModerationResult> ReviewTextAsync(string text, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        LastText = text;
        return Throws ? throw new InvalidOperationException("The moderation provider is down.") : Task.FromResult(new ModerationResult(Outcome));
    }

    public Task<ModerationResult> ReviewImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) =>
        Task.FromResult(new ModerationResult(Outcome));
}

/// <summary>Una copia de <c>Knock.Messaging.Api</c> con MongoDB (documentdb-local) y Redis reales, Mercado de mentira y moderación sustituible. Dos copias sobre el mismo Redis son «dos réplicas».</summary>
internal sealed class EnvioFactory(IReadOnlyDictionary<string, string?> settings, IContentModerator? moderator, string environment = "Production") : WebApplicationFactory<MessagingApi::Program>
{
    public HubConnection Connect(string token) => ChatHubClient.Connect(Server, token);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment(environment);
        builder.UseSetting("ForwardedHeaders:KnownNetworks:0", "10.0.0.0/8"); // spec 035, R-09: fuera de Development hay que fijar las redes de confianza
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
        builder.ConfigureServices(services =>
        {
            if (moderator is not null)
            {
                services.RemoveAll<IContentModerator>();
                services.AddSingleton(moderator);
            }
        });
    }
}

/// <summary>
/// Una conversación ficticia entre un cliente y un técnico (colección <c>conversaciones</c> con la forma de la spec 030, §6) sobre una base propia en documentdb-local, con Mercado de
/// mentira y una o dos copias del servicio. Al terminar se borra lo creado. Solo datos ficticios.
/// </summary>
internal sealed class ConversacionesScenario : IAsyncDisposable
{
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(10);

    private readonly List<EnvioFactory> _copies = [];
    private readonly List<HubConnection> _connections = [];
    private readonly MongoClient _mongo;
    private readonly string _databaseName = "s030_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
    private readonly string _mongoConnection;

    private ConversacionesScenario(DocumentDbFixture documentDb)
    {
        _mongoConnection = documentDb.ConnectionString;
        _mongo = new MongoClient(_mongoConnection);
    }

    public string Client { get; } = Guid.NewGuid().ToString("D");

    public string Technician { get; } = Guid.NewGuid().ToString("D");

    public string Outsider { get; } = Guid.NewGuid().ToString("D");

    public FakeMarket Market { get; private set; } = null!;

    public FakeModerator Moderator { get; } = new();

    public string ConversationId { get; private set; } = string.Empty;

    public IMongoDatabase Database => _mongo.GetDatabase(_databaseName);

    public static async Task<ConversacionesScenario> StartAsync(bool open = true, bool chosen = false, string? closure = null)
    {
        var scenario = new ConversacionesScenario(await DocumentDbFixture.SharedAsync());
        scenario.Market = await FakeMarket.StartAsync();
        scenario.Permit(open, chosen, closure);
        scenario.ConversationId = await scenario.SeedAsync();
        return scenario;
    }

    /// <summary>Cambia lo que Mercado contesta a quien sea participante del par; un ajeno sigue recibiendo 404.</summary>
    public void Permit(bool open, bool chosen, string? closure = null) =>
        Market.Permission = (sub, solicitudId, _) => Task.FromResult(
            sub == Client || sub == Technician
                ? new PermissionAnswer(
                    200,
                    new PermisoConversacion(solicitudId, "tec-1", Client, Technician, RoleOf(sub), open, closure, chosen, VisitFor(chosen), "Plomería", "Centro", new TecnicoPublicoConversacion("Técnico de prueba", null)))
                : new PermissionAnswer(404));

    private string RoleOf(string sub) => sub == Client ? "cliente" : "tecnico";

    private static string? VisitFor(bool chosen) => chosen ? "visita-1" : null;

    public async Task<EnvioFactory> CopyAsync(IReadOnlyDictionary<string, string?>? overrides = null, bool defaultModerator = false, string environment = "Production")
    {
        var redis = await RedisTestFixture.SharedAsync();
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:KnockRedis"] = redis.ConnectionString,
            ["ConnectionStrings:KnockMongo"] = _mongoConnection,
            ["Mongo:Databases:Messaging"] = _databaseName,
            ["Jwt:Issuer"] = GatewayTestSupport.Issuer,
            ["Jwt:Audience"] = GatewayTestSupport.Audience,
            ["Jwt:SigningKey"] = GatewayTestSupport.SigningKeyBase64,
            ["RateLimiting:Global:PermitLimit"] = "1000000",
            ["RateLimiting:Ready:PermitLimit"] = "1000000",
            ["Chat:Habilitado"] = "true",
            ["Services:Market:BaseUrl"] = Market.BaseUrl,
        };
        foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
        {
            values[key] = value;
        }

        var copy = new EnvioFactory(values, defaultModerator ? null : Moderator, environment);
        _copies.Add(copy);
        return copy;
    }

    public static HttpClient ClientFor(EnvioFactory copy, string? sub)
    {
        ArgumentNullException.ThrowIfNull(copy);
        var client = copy.CreateClient();
        if (sub is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GatewayTestSupport.IssueToken(sub: sub));
        }

        return client;
    }

    public async Task<HttpResponseMessage> SendAsync(EnvioFactory copy, string? sub, string text, string? conversationId = null)
    {
        using var client = ClientFor(copy, sub);
        return await client.PostAsJsonAsync(new Uri($"/api/v1/conversaciones/{conversationId ?? ConversationId}/mensajes", UriKind.Relative), new { texto = text });
    }

    public async Task<HubConnection> ConnectAsync(EnvioFactory copy, string sub)
    {
        var connection = copy.Connect(GatewayTestSupport.IssueToken(sub: sub));
        _connections.Add(connection);
        await connection.StartAsync();
        return connection;
    }

    /// <summary>Todos los eventos con ese nombre que reciba la conexión, en orden.</summary>
    public static ChannelReader<JsonElement> Listen(HubConnection connection, string eventName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var channel = Channel.CreateUnbounded<JsonElement>();
        connection.On<JsonElement>(eventName, message => channel.Writer.TryWrite(message));
        return channel.Reader;
    }

    public static async Task<JsonElement> NextAsync(ChannelReader<JsonElement> inbox)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        using var timeout = new CancellationTokenSource(_wait);
        return await inbox.ReadAsync(timeout.Token);
    }

    public static async Task<bool> NothingArrivesAsync(ChannelReader<JsonElement> inbox, TimeSpan time)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        using var timeout = new CancellationTokenSource(time);
        try
        {
            await inbox.ReadAsync(timeout.Token);
            return false;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }

    public static async Task<JsonElement> InvokeAsync(HubConnection connection, string method, string argument)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return await connection.InvokeAsync<JsonElement>(method, argument);
    }

    public Task<List<BsonDocument>> MessagesAsync() => Database.GetCollection<BsonDocument>("mensajes").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();

    public Task<List<BsonDocument>> OutboxAsync() => Database.GetCollection<BsonDocument>("outbox").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();

    public Task<BsonDocument> ConversationAsync() =>
        Database.GetCollection<BsonDocument>("conversaciones").Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(ConversationId))).SingleAsync();

    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }

        foreach (var copy in _copies)
        {
            await copy.DisposeAsync();
        }

        await Market.DisposeAsync();
        await _mongo.DropDatabaseAsync(_databaseName);
        _mongo.Dispose();
    }

    private async Task<string> SeedAsync()
    {
        var now = DateTime.UtcNow;
        var id = ObjectId.GenerateNewId();
        await Database.GetCollection<BsonDocument>("conversaciones").InsertOneAsync(new BsonDocument
        {
            { "_id", id },
            { "solicitudId", ObjectId.GenerateNewId().ToString() },
            { "tecnicoId", "tec-1" },
            { "clienteId", Client },
            { "tecnicoUsuarioId", Technician },
            { "servicio", "Plomería" },
            { "coloniaNombre", "Centro" },
            { "tecnicoPublico", new BsonDocument { { "nombre", "Técnico de prueba" }, { "fotoUrl", BsonNull.Value } } },
            { "estado", "abierta" },
            { "elegido", false },
            { "ultimoMensajeEn", now },
            { "noLeidosCliente", 0 },
            { "noLeidosTecnico", 0 },
            { "creadoEn", now },
            { "actualizadoEn", now },
        });
        return id.ToString();
    }
}
