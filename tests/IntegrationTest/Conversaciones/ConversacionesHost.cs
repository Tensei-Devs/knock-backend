extern alias MessagingApi;

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Knock.Contracts.Conversations;
using Knock.IntegrationTest.Support;
using MessagingApi::Knock.Messaging.Api.Modules.Conversaciones.Data;
using MessagingApi::Knock.Messaging.Api.Modules.Conversaciones.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.IntegrationTest.Conversaciones;

/// <summary>
/// Mercado de mentira para las pruebas de conversaciones (spec 030): contesta el permiso con lo que diga <see cref="Answer"/> y cuenta las llamadas. Por omisión responde como Mercado:
/// concede al cliente dueño (con el <c>tecnicoId</c> del par) y al técnico del par, y a cualquier otra persona, «no encontrado». Solo datos ficticios.
/// </summary>
internal sealed class FakePermisoDeConversacion : IPermisoDeConversacion
{
    private readonly List<PermisoConversacion> _grants = [];
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Respuesta de Mercado para (solicitudId, tecnicoId, sub); por omisión, la de los permisos concedidos con <see cref="Grant"/>.</summary>
    public Func<string, string?, string, ResultadoPermisoConversacion>? Answer { get; set; }

    public void Grant(PermisoConversacion permission) => _grants.Add(permission);

    public Task<ResultadoPermisoConversacion> ConsultarAsync(
        string solicitudId, string? tecnicoId, string? conversacionId, string subject, string bearerToken, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        if (Answer is not null)
        {
            return Task.FromResult(Answer(solicitudId, tecnicoId, subject));
        }

        foreach (var grant in _grants.Where(g => g.SolicitudId == solicitudId))
        {
            if (subject == grant.ClienteId && tecnicoId == grant.TecnicoId)
            {
                return Task.FromResult(new ResultadoPermisoConversacion(EstadoPermisoConversacion.Concedido, grant with { Rol = "cliente" }));
            }

            if (subject == grant.TecnicoUsuarioId)
            {
                return Task.FromResult(new ResultadoPermisoConversacion(EstadoPermisoConversacion.Concedido, grant with { Rol = "tecnico" }));
            }
        }

        return Task.FromResult(new ResultadoPermisoConversacion(EstadoPermisoConversacion.NoEncontrado));
    }
}

/// <summary>Entrega en vivo de mentira: guarda los avisos <c>noLeidos</c> que el servicio habría publicado por el hub.</summary>
internal sealed class RecordingNotificador : INotificadorEnVivo
{
    private readonly List<(string Subject, string ConversationId, int Unread, int Total)> _events = [];

    public IReadOnlyList<(string Subject, string ConversationId, int Unread, int Total)> Events
    {
        get
        {
            lock (_events)
            {
                return [.. _events];
            }
        }
    }

    public Task UnreadChangedAsync(string subject, string conversationId, int unread, int total, CancellationToken cancellationToken)
    {
        lock (_events)
        {
            _events.Add((subject, conversationId, unread, total));
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Una copia de <c>Knock.Messaging.Api</c> para las pruebas de conversaciones (spec 030): MongoDB y Redis reales con una base propia por host, Mercado de mentira y los índices de la spec
/// ya creados. Solo datos ficticios.
/// </summary>
internal sealed class ConversacionesFactory(IReadOnlyDictionary<string, string?> settings, FakePermisoDeConversacion permissions, RecordingNotificador live)
    : WebApplicationFactory<MessagingApi::Program>
{
    public IMongoDatabase Database => Services.GetRequiredService<IMongoClient>().GetDatabase(settings["Mongo:Databases:Messaging"]!);

    public static async Task<ConversacionesFactory> CreateAsync(FakePermisoDeConversacion permissions, RecordingNotificador live, IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var mongo = await DocumentDbFixture.SharedAsync();
        var redis = await RedisTestFixture.SharedAsync();
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:KnockRedis"] = redis.ConnectionString,
            ["ConnectionStrings:KnockMongo"] = mongo.ConnectionString,
            ["Mongo:Databases:Messaging"] = "messaging_it_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6)),
            ["Jwt:Issuer"] = GatewayTestSupport.Issuer,
            ["Jwt:Audience"] = GatewayTestSupport.Audience,
            ["Jwt:SigningKey"] = GatewayTestSupport.SigningKeyBase64,
            ["RateLimiting:Global:PermitLimit"] = "1000000",
            ["RateLimiting:Ready:PermitLimit"] = "1000000",
            ["Chat:Habilitado"] = "false",
            ["Services:Market:BaseUrl"] = "http://127.0.0.1:5234/",
        };
        foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
        {
            values[key] = value;
        }

        var factory = new ConversacionesFactory(values, permissions, live);
        await factory.Services.GetRequiredService<ConversacionesStore>().EnsureIndexesAsync(CancellationToken.None);
        await factory.Services.GetRequiredService<MensajesStore>().EnsureIndexesAsync(CancellationToken.None);
        return factory;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPermisoDeConversacion>();
            services.AddSingleton<IPermisoDeConversacion>(permissions);
            services.RemoveAll<INotificadorEnVivo>();
            services.AddSingleton<INotificadorEnVivo>(live);
        });
    }
}

/// <summary>
/// Un escenario de conversaciones: una solicitud ficticia con un cliente y un técnico, Mercado de mentira que los reconoce y una copia del servicio. <see cref="Client"/> y
/// <see cref="Technician"/> son los <c>sub</c> de cada parte; cualquier otro <c>sub</c> es ajeno. Solo datos ficticios.
/// </summary>
internal sealed class ConversacionesHost : IAsyncDisposable
{
    public const string ServiceName = "Plomería";
    public const string Neighborhood = "Colonia Demo";
    public const string TechnicianName = "Técnico Demo";
    public const string TechnicianPhoto = "https://imagenes.example.test/tecnico-demo.jpg";

    private readonly ConversacionesFactory _factory;
    private readonly HttpClient _client;

    private ConversacionesHost(ConversacionesFactory factory, FakePermisoDeConversacion permissions, RecordingNotificador live)
    {
        _factory = factory;
        Permissions = permissions;
        Live = live;
        _client = factory.CreateClient();
    }

    public string Client { get; } = Guid.NewGuid().ToString("D");

    public string Technician { get; } = Guid.NewGuid().ToString("D");

    public string RequestId { get; } = ObjectId.GenerateNewId().ToString();

    public string TechnicianId { get; } = ObjectId.GenerateNewId().ToString();

    public FakePermisoDeConversacion Permissions { get; }

    public RecordingNotificador Live { get; }

    public IMongoCollection<BsonDocument> Conversations => _factory.Database.GetCollection<BsonDocument>(ConversacionesStore.ConversationsCollection);

    public IMongoCollection<BsonDocument> Messages => _factory.Database.GetCollection<BsonDocument>(MensajesStore.MessagesCollection);

    public IMongoDatabase Database => _factory.Database;

    public static async Task<ConversacionesHost> StartAsync(IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var permissions = new FakePermisoDeConversacion();
        var live = new RecordingNotificador();
        var host = new ConversacionesHost(await ConversacionesFactory.CreateAsync(permissions, live, overrides), permissions, live);
        host.Permissions.Grant(host.Permission());
        return host;
    }

    /// <summary>El permiso de la solicitud del escenario; <paramref name="open"/> y <paramref name="chosen"/> son lo que Mercado diría.</summary>
    public PermisoConversacion Permission(bool open = true, bool chosen = false, string? closeReason = null) => new(
        RequestId,
        TechnicianId,
        Client,
        Technician,
        "cliente",
        open,
        closeReason,
        chosen,
        chosen ? ObjectId.GenerateNewId().ToString() : null,
        ServiceName,
        Neighborhood,
        new TecnicoPublicoConversacion(TechnicianName, TechnicianPhoto));

    public static string Token(string sub) => GatewayTestSupport.IssueToken(sub: sub);

    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return JsonElement.Parse(await response.Content.ReadAsStringAsync());
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? sub, object? body = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (sub is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(sub));
        }

        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        return await _client.SendAsync(request);
    }

    /// <summary>Pide crear la conversación como <paramref name="sub"/>; el cliente manda el <c>tecnicoId</c> y el técnico lo omite.</summary>
    public Task<HttpResponseMessage> CreateAsync(string sub, string? technicianId = null) =>
        SendAsync(HttpMethod.Post, "/api/v1/conversaciones", sub, new { solicitudId = RequestId, tecnicoId = technicianId });

    /// <summary>Siembra una conversación entre el cliente y el técnico del escenario con lo que se indique, y devuelve su <c>conversacionId</c>.</summary>
    public async Task<string> SeedConversationAsync(
        DateTime? lastAt = null, int unreadClient = 0, int unreadTechnician = 0, string? client = null, string? technician = null, bool open = true, ObjectId? id = null, string? request = null)
    {
        var conversationId = id ?? ObjectId.GenerateNewId();
        var at = lastAt ?? DateTime.UtcNow.AddMinutes(-5);
        await Conversations.InsertOneAsync(new BsonDocument
        {
            { "_id", conversationId },
            { "solicitudId", request ?? ObjectId.GenerateNewId().ToString() },
            { "tecnicoId", TechnicianId },
            { "clienteId", client ?? Client },
            { "tecnicoUsuarioId", technician ?? Technician },
            { "visitaId", BsonNull.Value },
            { "servicio", ServiceName },
            { "coloniaNombre", Neighborhood },
            { "tecnicoPublico", new BsonDocument { { "nombre", TechnicianName }, { "fotoUrl", TechnicianPhoto } } },
            { "estado", open ? "abierta" : "cerrada" },
            { "motivoCierre", open ? BsonNull.Value : "request_cancelled" },
            { "elegido", false },
            { "ultimoMensajeEn", at },
            { "noLeidosCliente", unreadClient },
            { "noLeidosTecnico", unreadTechnician },
            { "creadoEn", at },
            { "actualizadoEn", at },
        });
        return conversationId.ToString();
    }

    /// <summary>Siembra un mensaje de <paramref name="authorRole"/> en la conversación; <c>autorId</c> lleva un <c>sub</c> que nunca debe salir en las respuestas.</summary>
    public async Task SeedMessageAsync(string conversationId, string authorRole, DateTime at, string text = "mensaje de prueba")
    {
        await Messages.InsertOneAsync(new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "mensajeId", Guid.CreateVersion7().ToString("D") },
            { "conversacionId", ObjectId.Parse(conversationId) },
            { "autorRol", authorRole },
            { "autorId", authorRole == "cliente" ? Client : Technician },
            { "texto", text },
            { "contactoOculto", false },
            { "creadoEn", at },
        });
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }
}
