extern alias MarketApi;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Requests;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Modules.Catalog.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;
using Xunit;

namespace Knock.IntegrationTest.Catalog;

/// <summary>
/// Spec 027 T-05 y T-08: casos de interpretación, filtros, cobertura y caché de 30 s de punta a punta (HTTP, MongoDB y Redis reales, sin gateway).
/// El conteo de términos sin resultado (CA-03 y CA-14) depende del filtro real de la spec 024 y no se afirma aquí.
/// </summary>
public sealed class Spec027BusquedaCierreTests
{
    private static readonly string[] _renamedWords = ["ramona", "nuñez", "tuberias", "centella"];
    private static readonly string[] _plumbing = ["plo"];

    [Fact]
    public async Task Sinonimo_Con_Error_Y_Variantes_Dan_El_Mismo_Resultado_CA_027_02()
    {
        using var factory = await MarketApiFactory.CreateAsync();
        var colonia = await SeedAsync(factory);
        await InsertTechnicianAsync(factory, colonia, "Taller Uno", "plo");
        using var client = WaitlistHttp.SignedIn(factory);
        var reference = await SearchAsync(client, "boiler", colonia);
        foreach (var variant in new[] { "Bóiler", "BOILER", "boiler!!" })
        {
            var body = await SearchAsync(client, variant, colonia);
            body.GetProperty("interpretacion").GetProperty("estado").GetString().Should().Be("detectado", variant);
            body.GetProperty("items").GetArrayLength().Should().Be(reference.GetProperty("items").GetArrayLength(), variant);
        }
        var typo = await SearchAsync(client, "boilr", colonia);
        typo.GetProperty("interpretacion").GetProperty("estado").GetString().Should().Be("detectado");
    }

    [Fact]
    public async Task Texto_Sin_Oficio_Es_No_Entendido_Y_Vacio_CA_027_03()
    {
        using var factory = await MarketApiFactory.CreateAsync();
        var colonia = await SeedAsync(factory);
        await InsertTechnicianAsync(factory, colonia, "Taller Uno", "plo");
        using var client = WaitlistHttp.SignedIn(factory);
        var body = await SearchAsync(client, "quiero pastel", colonia);
        body.GetProperty("interpretacion").GetProperty("estado").GetString().Should().Be("no_entendido");
        body.GetProperty("items").GetArrayLength().Should().Be(0);
        body.TryGetProperty("nextCursor", out var cursor).Should().BeTrue();
        cursor.ValueKind.Should().BeOneOf(JsonValueKind.Null, JsonValueKind.Undefined);
        var stored = await factory.Database.GetCollection<BsonDocument>("busquedas_sin_resultado").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        stored.Should().OnlyContain(x => !x.Contains("sub") && !x.Contains("usuarioId"));
    }

    [Fact]
    public async Task Nombre_Del_Negocio_Aparece_Y_Suspendido_U_Oculto_No_CA_027_04()
    {
        using var factory = await MarketApiFactory.CreateAsync();
        var colonia = await SeedAsync(factory);
        var visible = await InsertTechnicianAsync(factory, colonia, "Taller El Guero", "plo", words: ["taller", "el", "guero"]);
        await InsertTechnicianAsync(factory, colonia, "Taller El Guero Suspendido", "plo", words: ["taller", "el", "guero"], state: "suspendido");
        await InsertTechnicianAsync(factory, colonia, "Taller El Guero Oculto", "plo", words: ["taller", "el", "guero"], hidden: true);
        using var client = WaitlistHttp.SignedIn(factory);
        var body = await SearchAsync(client, "guero", colonia);
        var items = body.GetProperty("items").EnumerateArray().ToArray();
        items.Should().HaveCount(1);
        items[0].GetProperty("id").GetString().Should().Be(visible.ToString());
    }

    [Fact]
    public async Task Dos_Oficios_Por_Igual_Son_Ambiguos_CA_027_05()
    {
        using var factory = await MarketApiFactory.CreateAsync();
        var colonia = await SeedAsync(factory);
        await InsertTechnicianAsync(factory, colonia, "Taller Uno", "plo"); // crea el índice 2dsphere que exige $geoNear
        using var client = WaitlistHttp.SignedIn(factory);
        var body = await SearchAsync(client, "luz agua", colonia);
        var interpretation = body.GetProperty("interpretacion");
        interpretation.GetProperty("estado").GetString().Should().Be("ambiguo");
        interpretation.GetProperty("oficiosSugeridos").EnumerateArray().Select(x => x.GetProperty("clave").GetString())
            .Should().BeEquivalentTo("plo", "ele");
    }

    [Fact]
    public async Task Filtro_De_Oficio_Manda_Sobre_La_Deteccion_CA_027_06()
    {
        using var factory = await MarketApiFactory.CreateAsync();
        var colonia = await SeedAsync(factory);
        await InsertTechnicianAsync(factory, colonia, "Plomero Demo", "plo");
        var electrician = await InsertTechnicianAsync(factory, colonia, "Electricista Demo", "ele");
        using var client = WaitlistHttp.SignedIn(factory);
        using var response = await client.GetAsync(new Uri($"/api/v1/busqueda?q=boiler&coloniaId={colonia}&oficio=ele", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("interpretacion").GetProperty("estado").GetString().Should().Be("elegido");
        body.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetString()).Should().BeEquivalentTo(electrician.ToString());
    }

    [Fact]
    public async Task Solo_Verificados_Excluye_Los_No_Verificados_CA_027_07()
    {
        using var factory = await MarketApiFactory.CreateAsync();
        var colonia = await SeedAsync(factory);
        await InsertTechnicianAsync(factory, colonia, "Sin Verificar", "plo", verification: "ninguno");
        await InsertTechnicianAsync(factory, colonia, "Verificado", "plo", verification: "basica");
        using var client = WaitlistHttp.SignedIn(factory);
        using var response = await client.GetAsync(new Uri($"/api/v1/busqueda?q=boiler&coloniaId={colonia}&soloVerificados=true", UriKind.Relative));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToArray();
        items.Should().NotBeEmpty();
        items.Should().OnlyContain(x => x.GetProperty("verificado").GetBoolean());
    }

    [Fact]
    public async Task Tecnico_Fuera_De_Su_Radio_No_Aparece_CA_027_09()
    {
        using var factory = await MarketApiFactory.CreateAsync();
        var colonia = await SeedAsync(factory);
        var near = await InsertTechnicianAsync(factory, colonia, "Cerca", "plo", longitude: 0.01, radius: 5);
        await InsertTechnicianAsync(factory, colonia, "Lejos", "plo", longitude: 0.5, radius: 1);
        using var client = WaitlistHttp.SignedIn(factory);
        var body = await SearchAsync(client, "boiler", colonia);
        body.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetString()).Should().BeEquivalentTo(near.ToString());
    }

    [Fact]
    public async Task Segunda_Busqueda_Se_Sirve_De_La_Cache_De_30_Segundos_CA_027_15()
    {
        // El host de pruebas lee la cadena de Redis después de registrar los servicios y por eso queda sin caché; se le da la caché real sobre una base de Redis propia
        // (la generación de la caché de búsqueda es global por base y otras pruebas la invalidan).
        var redis = await RedisTestFixture.SharedAsync();
        using var connection = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString + ",defaultDatabase=13");
        using var factory = await MarketApiFactory.CreateAsync(configureServices: services => services.AddSingleton<ISearchResponseCache>(
            new RedisCatalogCache(connection, Options.Create(new CatalogCacheOptions()), NullLogger<RedisCatalogCache>.Instance)));
        var colonia = await SeedAsync(factory);
        await InsertTechnicianAsync(factory, colonia, "Taller Uno", "plo");
        using var client = WaitlistHttp.SignedIn(factory);
        using var first = await client.GetAsync(new Uri($"/api/v1/busqueda?q=boiler&coloniaId={colonia}", UriKind.Relative));
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        first.Headers.CacheControl!.Private.Should().BeTrue();
        first.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(30));
        var firstBody = await first.Content.ReadAsStringAsync();
        await factory.Database.GetCollection<BsonDocument>("tecnicos").DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
        using var second = await client.GetAsync(new Uri($"/api/v1/busqueda?q=boiler&coloniaId={colonia}", UriKind.Relative));
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        second.Headers.CacheControl!.MaxAge.Should().Be(TimeSpan.FromSeconds(30));
        (await second.Content.ReadAsStringAsync()).Should().Be(firstBody, "la segunda llega de la caché sin consultar MongoDB");
    }

    [Fact]
    public async Task Un_Tecnico_Registrado_Por_La_Api_Se_Encuentra_Por_Su_Nombre_Y_Al_Cambiarlo_CA_027_04()
    {
        using var factory = await MarketApiFactory.CreateAsync();
        var colonia = await SeedAsync(factory);
        // El alta de técnico lee el nombre y el código postal de la colonia; la semilla de la búsqueda no los necesita.
        await factory.Database.GetCollection<BsonDocument>("colonias")
            .UpdateOneAsync(new BsonDocument("_id", colonia), Builders<BsonDocument>.Update.Set("nombre", "Colonia Ficticia").Set("cp", "42800").Set("municipio", "Municipio Ficticio").Set("estado", "Estado Ficticio"));
        await factory.Database.GetCollection<BsonDocument>("tecnicos")
            .Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Geo2DSphere("ubicacionBase")));
        using var client = WaitlistHttp.SignedIn(factory);
        using var registered = await client.PostAsJsonAsync(new Uri("/api/v1/tecnicos", UriKind.Relative), new
        {
            nombre = "Ramona Núñez",
            oficios = _plumbing,
            atencion = "domicilio",
            radioKm = 5,
            coloniaId = colonia.ToString(),
            perfil = new { nombreNegocio = "Destapes Relámpago" },
            aceptaTerminos = true,
        });
        registered.StatusCode.Should().Be(HttpStatusCode.Created, await registered.Content.ReadAsStringAsync());

        var byBusiness = await SearchAsync(client, "relampago", colonia);
        using var renamed = await client.PutAsJsonAsync(new Uri("/api/v1/tecnicos/me", UriKind.Relative), new { nombreNegocio = "Tuberías Centella" });
        renamed.StatusCode.Should().Be(HttpStatusCode.OK, await renamed.Content.ReadAsStringAsync());
        var byNewName = await SearchAsync(client, "centella", colonia);
        var stored = await factory.Database.GetCollection<BsonDocument>("tecnicos").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();

        byBusiness.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("nombre").GetString()).Should().ContainSingle().Which.Should().Be("Ramona Núñez");
        byNewName.GetProperty("items").GetArrayLength().Should().Be(1);
        stored["palabrasBusqueda"].AsBsonArray.Select(x => x.AsString).Should().Equal(_renamedWords);
    }

    private static async Task<JsonElement> SearchAsync(HttpClient client, string query, ObjectId colonia)
    {
        using var response = await client.GetAsync(new Uri($"/api/v1/busqueda?q={Uri.EscapeDataString(query)}&coloniaId={colonia}", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK, query);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<ObjectId> InsertTechnicianAsync(MarketApiFactory factory, ObjectId colonia, string name, string trade, string[]? words = null,
        string state = "activo", bool hidden = false, string verification = "ninguno", double longitude = 0.01, int radius = 5)
    {
        var id = ObjectId.GenerateNewId();
        var collection = factory.Database.GetCollection<BsonDocument>("tecnicos");
        await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Geo2DSphere("ubicacionBase")));
        await collection.InsertOneAsync(new BsonDocument
        {
            ["_id"] = id,
            ["nombrePublico"] = name,
            ["usuarioId"] = Guid.NewGuid().ToString("D"),
            ["palabrasBusqueda"] = new BsonArray(words ?? [name.ToUpperInvariant().Replace(' ', '-')]),
            ["oficios"] = new BsonArray { trade },
            ["estado"] = state,
            ["oculto"] = hidden,
            ["nivelVerificacion"] = verification,
            ["coloniaBaseId"] = colonia.ToString(),
            ["calificacion"] = 4,
            ["totalResenas"] = 1,
            ["radioAtencionKm"] = radius,
            ["ubicacionBase"] = new BsonDocument { ["type"] = "Point", ["coordinates"] = new BsonArray { longitude, 0 } },
        });
        return id;
    }

    private static async Task<ObjectId> SeedAsync(MarketApiFactory factory)
    {
        var db = factory.Database;
        var id = ObjectId.GenerateNewId();
        await db.GetCollection<BsonDocument>("colonias").InsertOneAsync(new BsonDocument
        {
            ["_id"] = id,
            ["activo"] = true,
            ["centro"] = new BsonDocument { ["type"] = "Point", ["coordinates"] = new BsonArray { 0, 0 } },
        });
        await db.GetCollection<BsonDocument>("oficios").InsertManyAsync(
        [
            new BsonDocument { ["clave"] = "plo", ["nombre"] = "Plomería", ["nombreBusqueda"] = "PLOMERIA", ["modalidad"] = "domicilio", ["activo"] = true },
            new BsonDocument { ["clave"] = "ele", ["nombre"] = "Electricidad", ["nombreBusqueda"] = "ELECTRICIDAD", ["modalidad"] = "domicilio", ["activo"] = true },
        ]);
        await db.GetCollection<BsonDocument>("sinonimos").InsertManyAsync(
        [
            new BsonDocument { ["termino"] = "boiler", ["oficioClave"] = "plo", ["version"] = 1 },
            new BsonDocument { ["termino"] = "agua", ["oficioClave"] = "plo", ["version"] = 1 },
            new BsonDocument { ["termino"] = "luz", ["oficioClave"] = "ele", ["version"] = 1 },
        ]);
        await db.GetCollection<BsonDocument>("catalogo_meta").InsertOneAsync(new BsonDocument { ["_id"] = "sinonimos", ["version"] = 1 });
        return id;
    }
}
