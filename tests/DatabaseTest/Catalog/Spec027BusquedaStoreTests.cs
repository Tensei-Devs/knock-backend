using AwesomeAssertions;
using Knock.DatabaseTest.Support;
using Knock.IntegrationTest.Support;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Catalog.Data;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.DatabaseTest.Catalog;

public sealed class Spec027BusquedaStoreTests
{
    [Fact]
    public async Task Busca_Cobertura_Estado_Prefijo_Verificacion_CA_027_04_CA_027_07_CA_027_09()
    {
        await using var database = await CatalogDatabase.CreateAsync(seeded: false);
        await ApplyScriptAsync(database);
        var collection = database.Database.GetCollection<BsonDocument>("tecnicos");
        var near = Technician("cercano", 0.001, 2, "activo", false, "basica", "plo");
        var middle = Technician("medio", 0.01, 2, "activo", false, "completa", "plo");
        var unverified = Technician("sinverificar", 0.015, 2, "activo", false, "ninguno", "plo");
        var far = Technician("lejos", 0.05, 1, "activo", false, "ninguno", "plo");
        var nameOutside = Technician("especial", 0.4, 1, "activo", false, "basica", "ele");
        var suspended = Technician("suspendido", 0.001, 5, "suspendido", false, "basica", "plo");
        var hidden = Technician("oculto", 0.001, 5, "activo", true, "basica", "plo");
        var inactiveTrade = Technician("sinoficio", 0.001, 5, "activo", false, "basica", "inactivo");
        var differentColonia = Technician("otracolonia", 0.1, 1, "activo", false, "basica", "plo");
        differentColonia["coloniaBaseId"] = "otra";
        await collection.InsertManyAsync([near, middle, unverified, far, nameOutside, suspended, hidden, inactiveTrade, differentColonia]);
        var store = new BusquedaStore(database.Database, Options.Create(new KnockMongoOptions()));
        var candidates = await store.FindAsync("colonia-demo", 0, 0, ["esp"], "plo", ["plo", "ele"], false, CancellationToken.None);
        candidates.Select(x => x.Id).Should().BeEquivalentTo(near["_id"].ToString(), middle["_id"].ToString(), unverified["_id"].ToString(), nameOutside["_id"].ToString());
        candidates.Single(x => x.Id == nameOutside["_id"].ToString()).ServesColonia.Should().BeFalse();
        candidates.Where(x => x.ServesColonia).Select(x => x.DistanceKm).Should().BeInAscendingOrder();
        var verified = await store.FindAsync("colonia-demo", 0, 0, ["esp"], "plo", ["plo", "ele"], true, CancellationToken.None);
        verified.Should().HaveCount(3);
        verified.Should().OnlyContain(x => x.Verified);
        var shortPrefix = await store.FindAsync("otra", 0, 0, ["es"], null, ["plo", "ele"], false, CancellationToken.None);
        shortPrefix.Should().BeEmpty();
        var notPrefix = await store.FindAsync("otra", 0, 0, ["pecial"], null, ["plo", "ele"], false, CancellationToken.None);
        notPrefix.Should().BeEmpty();
        var noCenter = await store.FindAsync("colonia-demo", null, null, [], "plo", ["plo", "ele"], false, CancellationToken.None);
        noCenter.Select(x => x.Id).Should().BeEquivalentTo(near["_id"].ToString(), middle["_id"].ToString(), unverified["_id"].ToString(), far["_id"].ToString());
        var aggregate = database.Commands.First(x => x.Names.FirstOrDefault() == "aggregate" && x["aggregate"] == "tecnicos");
        aggregate["pipeline"].AsBsonArray[0].AsBsonDocument.Contains("$geoNear").Should().BeTrue();
        aggregate.Contains("maxTimeMS").Should().BeTrue();
    }

    [Fact]
    public async Task Conteo_Anonimo_Atomicamente_Y_Ttl_R_027_12()
    {
        await using var database = await CatalogDatabase.CreateAsync(seeded: false);
        await ApplyScriptAsync(database);
        var store = new BusquedaStore(database.Database, Options.Create(new KnockMongoOptions()));
        var instant = new DateTimeOffset(2026, 11, 14, 2, 0, 0, TimeSpan.Zero);
        await store.CountUnmatchedAsync("quiero pastel", instant, CancellationToken.None);
        await store.CountUnmatchedAsync("quiero pastel", instant, CancellationToken.None);
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => store.CountUnmatchedAsync("quiero pastel", instant, CancellationToken.None)));
        var collection = database.Database.GetCollection<BsonDocument>("busquedas_sin_resultado");
        var doc = await collection.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        doc.Names.Should().BeEquivalentTo("_id", "terminoNormalizado", "dia", "conteo", "creadoEn");
        doc["terminoNormalizado"].AsString.Should().Be("quiero pastel");
        doc["dia"].AsString.Should().Be("2026-11-13");
        doc["conteo"].ToInt32().Should().Be(12);
        doc["creadoEn"].ToUniversalTime().Should().Be(instant.UtcDateTime);
        (await MongoIndexes.DescribeAsync(database.Database, "busquedas_sin_resultado")).Should().Contain("creadoEn_1 creadoEn:1 - ttl:7776000");
    }

    private static async Task ApplyScriptAsync(CatalogDatabase database)
    {
        await using var tools = await MongoToolsContainer.StartAsync(await DocumentDbFixture.SharedAsync(), new Dictionary<string, string> { ["KNOCK_DB_MARKET"] = database.DatabaseName });
        var result = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", $"{MongoToolsContainer.DbDirectory}/027-catalog-busqueda.js");
        result.ExitCode.Should().Be(0, result.Output);
    }

    private static BsonDocument Technician(string word, double longitude, int radius, string state, bool hidden, string verification, string trade) => new()
    {
        ["_id"] = ObjectId.GenerateNewId(),
        ["nombrePublico"] = word,
        ["palabrasBusqueda"] = new BsonArray { word },
        ["ubicacionBase"] = new BsonDocument { ["type"] = "Point", ["coordinates"] = new BsonArray { longitude, 0 } },
        ["radioAtencionKm"] = radius,
        ["coloniaBaseId"] = "colonia-demo",
        ["estado"] = state,
        ["oculto"] = hidden,
        ["nivelVerificacion"] = verification,
        ["oficios"] = new BsonArray { trade },
        ["calificacion"] = 4,
        ["totalResenas"] = 1,
    };
}
