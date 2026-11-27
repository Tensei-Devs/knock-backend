extern alias MarketApi;

using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using MarketApi::Knock.Market.Api.Common.Crypto;
using MarketApi::Knock.Market.Api.Modules.Requests.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.SecurityTest.Support;

/// <summary>Quién llama en una prueba de acceso a una solicitud o a una visita.</summary>
internal enum Actor
{
    /// <summary>El cliente dueño de la solicitud y de la visita.</summary>
    Client,

    /// <summary>El técnico que cotizó y que queda en la visita.</summary>
    Chosen,

    /// <summary>Un técnico elegible al que le llegó la solicitud y todavía no cotiza.</summary>
    Invited,

    /// <summary>Otra persona con sesión válida que no es técnica.</summary>
    OtherPerson,

    /// <summary>Un técnico activo, de otro oficio y otra colonia, sin relación con la solicitud.</summary>
    OtherTechnician,
}

/// <summary>Una solicitud ficticia con sus personas: los <c>sub</c> de cada actor y los identificadores de lo sembrado.</summary>
internal sealed class MarketScene
{
    private readonly Dictionary<Actor, string> _subs = Enum.GetValues<Actor>().ToDictionary(actor => actor, _ => TokenForge.Sub());

    public ObjectId RequestId { get; } = ObjectId.GenerateNewId();

    public ObjectId QuoteId { get; } = ObjectId.GenerateNewId();

    public ObjectId VisitId { get; } = ObjectId.GenerateNewId();

    public ObjectId AdjustmentId { get; } = ObjectId.GenerateNewId();

    public ObjectId ColoniaId { get; } = ObjectId.GenerateNewId();

    public ObjectId ChosenTechnicianId { get; } = ObjectId.GenerateNewId();

    public ObjectId InvitedTechnicianId { get; } = ObjectId.GenerateNewId();

    public string Day { get; } = MarketAccessHost.LocalNow(TimeSpan.FromDays(3)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public string Sub(Actor actor) => _subs[actor];
}

/// <summary>
/// Un host de Mercado en <c>Production</c> con MongoDB y Redis reales para las pruebas de acceso entre personas a solicitudes y visitas. Se crea una sola vez; cada prueba siembra su propia
/// escena (personas e identificadores nuevos), con los documentos que la API lee de verdad, para que un rechazo se deba a la autorización y no a un dato que falta. Solo datos ficticios.
/// </summary>
internal sealed class MarketAccessHost
{
    public const string Address = "Calle Ficticia 77 Zq, Colonia Demo";
    public const string VisitCode = "4821";
    public const int Cap = 1000;

    private const string _trade = "plo";
    private const string _requests = "solicitudes";
    private const string _quotes = "cotizaciones";
    private const string _visits = "visitas";
    private const string _adjustments = "ajustes_precio";
    private const string _sends = "envios";
    private const string _outbox = "outbox";

    private static readonly Lazy<Task<MarketAccessHost>> _shared = new(StartAsync);

    private readonly WebApplicationFactory<MarketApi::Knock.Market.Api.Modules.Catalog.Data.CatalogStore> _app;
    private readonly string _databaseName;

    private MarketAccessHost(WebApplicationFactory<MarketApi::Knock.Market.Api.Modules.Catalog.Data.CatalogStore> app, string databaseName)
    {
        _app = app;
        _databaseName = databaseName;
        Client = app.CreateClient();
    }

    public HttpClient Client { get; }

    private IMongoDatabase Database => _app.Services.GetRequiredService<IMongoClient>().GetDatabase(_databaseName);

    private FieldCipher Cipher => _app.Services.GetRequiredService<FieldCipher>();

    public static Task<MarketAccessHost> SharedAsync() => _shared.Value;

    /// <summary>La hora de la Ciudad de México dentro de <paramref name="fromNow"/> (las citas se guardan como día y hora locales).</summary>
    public static DateTime LocalNow(TimeSpan fromNow) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow + fromNow, TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City"));

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? bearer, object? body)
    {
        using var content = body is null ? null : JsonContent.Create(body);
        return await SecureHttp.SendAsync(Client, method, path, content, bearer);
    }

    /// <summary>Siembra la solicitud en <c>cotizando</c>, con la dirección cifrada, la cotización del técnico elegido y los tres técnicos de la escena.</summary>
    public async Task<MarketScene> SeedRequestAsync()
    {
        var scene = new MarketScene();
        await SeedTechnicianAsync(scene.ChosenTechnicianId, scene.Sub(Actor.Chosen), _trade, scene.ColoniaId);
        await SeedTechnicianAsync(scene.InvitedTechnicianId, scene.Sub(Actor.Invited), _trade, scene.ColoniaId);
        await SeedTechnicianAsync(ObjectId.GenerateNewId(), scene.Sub(Actor.OtherTechnician), "ele", ObjectId.GenerateNewId());
        var now = DateTime.UtcNow;
        await InsertAsync(_requests, new BsonDocument
        {
            ["_id"] = scene.RequestId,
            ["clienteId"] = scene.Sub(Actor.Client),
            ["oficioClave"] = _trade,
            ["oficioNombre"] = "Plomería",
            ["modalidad"] = "domicilio",
            ["descripcion"] = "Gotea la llave del fregadero",
            ["fotos"] = new BsonArray(),
            ["dia"] = scene.Day,
            ["franja"] = "tarde",
            ["coloniaId"] = scene.ColoniaId.ToString(),
            ["coloniaNombre"] = "Centro",
            ["coloniaCp"] = "42800",
            ["direccionCifrada"] = Cipher.Encrypt(Address, $"solicitudes|direccion|{scene.RequestId}"),
            ["estado"] = "cotizando",
            ["oleadaActual"] = 1,
            ["venceEn"] = now.AddDays(1),
            ["creadoEn"] = now,
            ["actualizadoEn"] = now,
        });
        await InsertAsync(_quotes, new BsonDocument
        {
            ["_id"] = scene.QuoteId,
            ["solicitudId"] = scene.RequestId,
            ["tecnicoId"] = scene.ChosenTechnicianId,
            ["tecnicoUsuarioId"] = scene.Sub(Actor.Chosen),
            ["tipoPrecio"] = "fijo",
            ["precio"] = Cap,
            ["precioMin"] = BsonNull.Value,
            ["precioMax"] = BsonNull.Value,
            ["tope"] = Cap,
            ["requiereAprobacion"] = false,
            ["dia"] = scene.Day,
            ["hora"] = "10:00",
            ["nota"] = BsonNull.Value,
            ["estado"] = "enviada",
            ["creadoEn"] = now,
            ["actualizadoEn"] = now,
        });
        return scene;
    }

    /// <summary>La solicitud ya con cita: la cotización queda aceptada y la visita <c>confirmada</c> dentro de tres días, con su código cifrado.</summary>
    public async Task<MarketScene> SeedVisitAsync()
    {
        var scene = await SeedRequestAsync();
        await UpdateAsync(_requests, scene.RequestId, Builders<BsonDocument>.Update.Set("estado", "cita_confirmada"));
        await UpdateAsync(_quotes, scene.QuoteId, Builders<BsonDocument>.Update.Set("estado", "aceptada"));
        var now = DateTime.UtcNow;
        await InsertAsync(_visits, new BsonDocument
        {
            ["_id"] = scene.VisitId,
            ["solicitudId"] = scene.RequestId,
            ["cotizacionId"] = scene.QuoteId,
            ["clienteId"] = scene.Sub(Actor.Client),
            ["tecnicoUsuarioId"] = scene.Sub(Actor.Chosen),
            ["tecnicoId"] = scene.ChosenTechnicianId,
            ["modalidad"] = "domicilio",
            ["dia"] = scene.Day,
            ["hora"] = "10:00",
            ["precio"] = Cap,
            ["tope"] = Cap,
            ["estado"] = "confirmada",
            ["codigoCifrado"] = Cipher.Encrypt(VisitCode, $"visitas|codigo|{scene.VisitId}"),
            ["codigoVenceEn"] = now.AddDays(5),
            ["codigoUsado"] = false,
            ["intentos"] = 0,
            ["bloqueada"] = false,
            ["creadoEn"] = now,
            ["actualizadoEn"] = now,
        });
        return scene;
    }

    public Task SetVisitAsync(MarketScene scene, UpdateDefinition<BsonDocument> update) => UpdateAsync(_visits, scene.VisitId, update);

    public Task SetQuoteAsync(MarketScene scene, UpdateDefinition<BsonDocument> update) => UpdateAsync(_quotes, scene.QuoteId, update);

    public Task SeedAdjustmentAsync(MarketScene scene) =>
        InsertAsync(_adjustments, new BsonDocument
        {
            ["_id"] = scene.AdjustmentId,
            ["visitaId"] = scene.VisitId,
            ["cotizacionId"] = scene.QuoteId,
            ["clienteId"] = scene.Sub(Actor.Client),
            ["tecnicoUsuarioId"] = scene.Sub(Actor.Chosen),
            ["montoNuevo"] = Cap + 500,
            ["motivo"] = "Hubo que cambiar la manguera",
            ["estado"] = "pendiente",
            ["venceEn"] = DateTime.UtcNow.AddHours(12),
        });

    /// <summary>La solicitud le llegó al técnico invitado y sigue activa en su bandeja.</summary>
    public Task SeedSendAsync(MarketScene scene) =>
        InsertAsync(_sends, new BsonDocument
        {
            ["solicitudId"] = scene.RequestId,
            ["tecnicoId"] = scene.InvitedTechnicianId,
            ["tecnicoUsuarioId"] = scene.Sub(Actor.Invited),
            ["estado"] = "activo",
            ["oleada"] = 1,
            ["enviadoEn"] = DateTime.UtcNow,
        });

    /// <summary>Todo lo que una ruta de la escena podría escribir, como texto: dos fotos iguales significan que nada cambió.</summary>
    public async Task<string> SnapshotAsync(MarketScene scene)
    {
        var filter = Builders<BsonDocument>.Filter;
        var ids = new[] { scene.RequestId.ToString(), scene.VisitId.ToString(), scene.QuoteId.ToString(), scene.AdjustmentId.ToString() };
        (string Collection, FilterDefinition<BsonDocument> Filter)[] parts =
        [
            (_requests, filter.Eq("_id", scene.RequestId)),
            (_quotes, filter.Eq("solicitudId", scene.RequestId)),
            (_visits, filter.Eq("solicitudId", scene.RequestId)),
            (_adjustments, filter.Eq("visitaId", scene.VisitId)),
            (_sends, filter.Eq("solicitudId", scene.RequestId)),
            (_outbox, filter.In("agregadoId", ids) | filter.Eq("solicitudId", scene.RequestId)),
        ];
        var lines = new List<string>();
        foreach (var (collection, match) in parts)
        {
            var found = await Database.GetCollection<BsonDocument>(collection).Find(match).Sort(Builders<BsonDocument>.Sort.Ascending("_id")).ToListAsync();
            lines.Add(collection + ": " + string.Join(" | ", found.Select(document => document.ToJson())));
        }

        return string.Join('\n', lines);
    }

    private Task SeedTechnicianAsync(ObjectId id, string sub, string trade, ObjectId colonia) =>
        InsertAsync("tecnicos", new BsonDocument
        {
            ["_id"] = id,
            ["usuarioId"] = sub,
            ["nombre"] = "Técnico Demo",
            ["estado"] = "activo",
            ["oculto"] = false,
            ["oficios"] = new BsonArray { trade },
            ["coloniaBaseId"] = colonia.ToString(),
            ["radioAtencionKm"] = 5,
            ["nivelVerificacion"] = "basica",
        });

    private Task InsertAsync(string collection, BsonDocument document) => Database.GetCollection<BsonDocument>(collection).InsertOneAsync(document);

    private Task UpdateAsync(string collection, ObjectId id, UpdateDefinition<BsonDocument> update) =>
        Database.GetCollection<BsonDocument>(collection).UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), update);

    private static async Task<MarketAccessHost> StartAsync()
    {
        var name = "market_acc_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        var settings = new Dictionary<string, string?>
        {
            ["Security:FieldEncryption:ActiveVersion"] = "1",
            ["Security:FieldEncryption:Keys:1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        };
        foreach (var module in new[] { "Catalog", "Requests", "Trust", "Tecnicos", "Reviews" })
        {
            settings["Mongo:Databases:" + module] = name;
        }

        var factory = await SecurityMarketFactory.CreateAsync(settings);
        // Sin Blob en las pruebas: las fotos del trabajo se dan por listas y las de la solicitud (aquí no hay) salen sin enlaces.
        var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IVisitPhotos>(new ReadyVisitPhotos());
            services.AddSingleton<IRequestPhotos>(new UnavailableRequestPhotos());
        }));
        var host = new MarketAccessHost(app, name);
        await MongoCollections.CreateIfMissingAsync(host.Database, _requests, _quotes, _visits, _adjustments, _sends, _outbox, "tareas_programadas");
        return host;
    }

    private sealed class ReadyVisitPhotos : IVisitPhotos
    {
        public Task<bool> AreReadyForAsync(string ownerId, IReadOnlyList<string> photoIds, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
