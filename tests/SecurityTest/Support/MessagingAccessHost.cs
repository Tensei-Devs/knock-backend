extern alias MessagingApi;

using System.Globalization;
using System.Security.Cryptography;
using Knock.Contracts.Conversations;
using Knock.Contracts.Moderation;
using Knock.IntegrationTest.Support;
using MessagingApi::Knock.Messaging.Api.Modules.Conversaciones.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.SecurityTest.Support;

/// <summary>Una conversación ficticia entre dos personas, con un mensaje del técnico que el cliente no ha leído.</summary>
internal sealed record SeededChat(ObjectId Id, string ClientSub, string TechnicianSub);

/// <summary>
/// Host de Mensajería con MongoDB y Redis reales para las pruebas de acceso entre personas. Aquí Mercado SIEMPRE concede el permiso de conversación y la moderación aprueba: así, si una persona
/// ajena es rechazada, es porque Mensajería comprueba quién participa. Cada instancia usa su propia base. Solo datos ficticios.
/// </summary>
internal sealed class MessagingAccessHost : IAsyncDisposable
{
    public const string MessageText = "Texto privado Zq7 de prueba";

    private const string _conversations = "conversaciones";
    private const string _messages = "mensajes";
    private const string _notices = "avisos";
    private const string _outbox = "outbox";

    private readonly SecurityMessagingFactory _factory;
    private readonly WebApplicationFactory<MessagingApi::Program> _app;
    private readonly string _databaseName;

    private MessagingAccessHost(SecurityMessagingFactory factory, WebApplicationFactory<MessagingApi::Program> app, string databaseName)
    {
        _factory = factory;
        _app = app;
        _databaseName = databaseName;
        Client = app.CreateClient();
    }

    public HttpClient Client { get; }

    private IMongoDatabase Database => _app.Services.GetRequiredService<IMongoClient>().GetDatabase(_databaseName);

    public static async Task<MessagingAccessHost> StartAsync()
    {
        var mongo = await DocumentDbFixture.SharedAsync();
        var name = "messaging_acc_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        var factory = await SecurityMessagingFactory.CreateAsync(new Dictionary<string, string?>
        {
            ["ConnectionStrings:KnockMongo"] = mongo.ConnectionString,
            ["Mongo:Databases:Messaging"] = name,
        });
        var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPermisoDeConversacion>();
            services.AddSingleton<IPermisoDeConversacion>(new AlwaysGrants());
            services.RemoveAll<IContentModerator>();
            services.AddSingleton<IContentModerator>(new AlwaysAllows());
        }));
        var host = new MessagingAccessHost(factory, app, name);
        await MongoCollections.CreateIfMissingAsync(host.Database, _conversations, _messages, _notices, _outbox);
        return host;
    }

    public async Task<SeededChat> SeedChatAsync()
    {
        var chat = new SeededChat(ObjectId.GenerateNewId(), TokenForge.Sub(), TokenForge.Sub());
        var at = DateTime.UtcNow.AddMinutes(-5);
        await Database.GetCollection<BsonDocument>(_conversations).InsertOneAsync(new BsonDocument
        {
            ["_id"] = chat.Id,
            ["solicitudId"] = ObjectId.GenerateNewId().ToString(),
            ["tecnicoId"] = ObjectId.GenerateNewId().ToString(),
            ["clienteId"] = chat.ClientSub,
            ["tecnicoUsuarioId"] = chat.TechnicianSub,
            ["visitaId"] = BsonNull.Value,
            ["servicio"] = "Plomería",
            ["coloniaNombre"] = "Centro",
            ["tecnicoPublico"] = new BsonDocument { ["nombre"] = "Técnico Demo", ["fotoUrl"] = BsonNull.Value },
            ["estado"] = "abierta",
            ["motivoCierre"] = BsonNull.Value,
            ["elegido"] = false,
            ["ultimoMensajeEn"] = at,
            ["noLeidosCliente"] = 1,
            ["noLeidosTecnico"] = 0,
            ["creadoEn"] = at,
            ["actualizadoEn"] = at,
        });
        await Database.GetCollection<BsonDocument>(_messages).InsertOneAsync(new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["mensajeId"] = Guid.CreateVersion7().ToString("D"),
            ["conversacionId"] = chat.Id,
            ["autorRol"] = "tecnico",
            ["autorId"] = chat.TechnicianSub,
            ["texto"] = MessageText,
            ["contactoOculto"] = false,
            ["creadoEn"] = at,
        });
        return chat;
    }

    /// <summary>Siembra un aviso sin leer de <paramref name="sub"/> y devuelve su identificador.</summary>
    public async Task<ObjectId> SeedNoticeAsync(string sub)
    {
        var id = ObjectId.GenerateNewId();
        await Database.GetCollection<BsonDocument>(_notices).InsertOneAsync(new BsonDocument
        {
            ["_id"] = id,
            ["usuarioId"] = sub,
            ["tipo"] = "visitas",
            ["comoRol"] = "cliente",
            ["titulo"] = "Aviso ficticio",
            ["texto"] = "Texto del aviso ficticio",
            ["leido"] = false,
            ["creadoEn"] = DateTime.UtcNow,
        });
        return id;
    }

    public async Task<bool> IsReadAsync(ObjectId noticeId)
    {
        var notice = await Database.GetCollection<BsonDocument>(_notices).Find(Builders<BsonDocument>.Filter.Eq("_id", noticeId)).SingleAsync();
        return notice["leido"].AsBoolean;
    }

    /// <summary>La conversación, sus mensajes y el buzón como texto: dos fotos iguales significan que nada cambió.</summary>
    public async Task<string> SnapshotAsync(SeededChat chat)
    {
        var filter = Builders<BsonDocument>.Filter;
        var conversation = await Database.GetCollection<BsonDocument>(_conversations).Find(filter.Eq("_id", chat.Id)).SingleAsync();
        var messages = await Database.GetCollection<BsonDocument>(_messages)
            .Find(filter.Eq("conversacionId", chat.Id)).Sort(Builders<BsonDocument>.Sort.Ascending("_id")).ToListAsync();
        var pending = await Database.GetCollection<BsonDocument>(_outbox).CountDocumentsAsync(filter.Empty);
        var lines = new List<string> { conversation.ToJson(), "outbox: " + pending.ToString(CultureInfo.InvariantCulture) };
        lines.AddRange(messages.Select(message => message.ToJson()));
        return string.Join('\n', lines);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
        await _factory.DisposeAsync();
    }

    private sealed class AlwaysGrants : IPermisoDeConversacion
    {
        public Task<ResultadoPermisoConversacion> ConsultarAsync(
            string solicitudId, string? tecnicoId, string? conversacionId, string subject, string bearerToken, CancellationToken cancellationToken)
        {
            var permission = new PermisoConversacion(
                solicitudId, tecnicoId ?? "tec-demo", subject, subject, "cliente", true, null, false, null, "Plomería", "Centro", new TecnicoPublicoConversacion("Técnico Demo", null));
            return Task.FromResult(new ResultadoPermisoConversacion(EstadoPermisoConversacion.Concedido, permission));
        }
    }

    private sealed class AlwaysAllows : IContentModerator
    {
        public Task<ModerationResult> ReviewTextAsync(string text, CancellationToken cancellationToken) => Task.FromResult(new ModerationResult(ModerationOutcome.Allowed));

        public Task<ModerationResult> ReviewImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) => Task.FromResult(new ModerationResult(ModerationOutcome.Allowed));
    }
}
