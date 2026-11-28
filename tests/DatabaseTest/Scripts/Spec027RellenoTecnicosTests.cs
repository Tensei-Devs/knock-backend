using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.DatabaseTest.Support;
using Knock.IntegrationTest.Support;
using Knock.Market.Api.Modules.Tecnicos.Domain;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.DatabaseTest.Scripts;

/// <summary>
/// <c>db/relleno/tecnicos-busqueda.js</c> (spec 027, R-06, y spec 051, R-14) con <c>mongosh</c> real contra documentdb-local: la simulación no escribe,
/// al aplicar deja las mismas palabras que calcula el servicio, repetirlo no cambia nada y las cuentas eliminadas no se tocan. Solo datos ficticios.
/// </summary>
public sealed class Spec027RellenoTecnicosTests
{
    private const string _script = MongoToolsContainer.DbDirectory + "/relleno/tecnicos-busqueda.js";

    [Fact]
    public async Task Simula_Sin_Escribir_Aplica_Como_El_Servicio_Y_Es_Idempotente_CA_027_04()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        var database = "s027_relleno_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        using var client = new MongoClient(documentDb.ConnectionString);
        var market = client.GetDatabase(database);
        var technicians = market.GetCollection<BsonDocument>("tecnicos");
        var neighborhood = ObjectId.GenerateNewId();
        var old = ObjectId.GenerateNewId();
        var hidden = ObjectId.GenerateNewId();
        var removed = ObjectId.GenerateNewId();
        const string name = "  Ramón  Núñez-Ibáñez ";
        const string business = "Taller «El Güero» 24/7 & Frenos";
        try
        {
            await market.GetCollection<BsonDocument>("colonias").InsertOneAsync(new BsonDocument { ["_id"] = neighborhood, ["nombre"] = "Colonia Ficticia" });
            await technicians.InsertManyAsync(
            [
                new BsonDocument
                {
                    ["_id"] = old, ["usuarioId"] = Guid.NewGuid().ToString("D"), ["estado"] = "activo", ["nombre"] = name, ["nombreNegocio"] = business,
                    ["coloniaBaseId"] = neighborhood.ToString(), ["reputacion"] = new BsonDocument { ["promedio"] = 4.5, ["total"] = 2 },
                },
                // Con todas sus opiniones ocultas: la calificación vieja debe desaparecer.
                new BsonDocument
                {
                    ["_id"] = hidden, ["usuarioId"] = Guid.NewGuid().ToString("D"), ["estado"] = "activo", ["nombre"] = "Ana Pruebas",
                    ["calificacion"] = 5.0, ["reputacion"] = new BsonDocument { ["promedio"] = BsonNull.Value, ["total"] = 0 },
                },
                new BsonDocument { ["_id"] = removed, ["usuarioId"] = Guid.NewGuid().ToString("D"), ["estado"] = "eliminado", ["nombre"] = "Técnico eliminado" },
            ]);
            await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string> { ["KNOCK_DB_MARKET"] = database });
            var before = await AllAsync(technicians);

            var dryRun = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", _script);
            var afterDryRun = await AllAsync(technicians);
            var applied = await tools.RunAsync("sh", "-c", $"KNOCK_APLICAR=si mongosh --nodb --quiet --file {_script}");
            var again = await tools.RunAsync("sh", "-c", $"KNOCK_APLICAR=si mongosh --nodb --quiet --file {_script}");

            dryRun.ExitCode.Should().Be(0, dryRun.Output);
            dryRun.Output.Should().Contain("SIMULACIÓN").And.Contain("Técnicos con cambios pendientes: 2");
            dryRun.Output.Should().NotContain("Ramón").And.NotContain("Güero").And.NotContain("Ana Pruebas", "la salida no lleva nombres").And.NotContain("Kn0ck");
            afterDryRun.Should().Equal(before, "la simulación no escribe");
            applied.ExitCode.Should().Be(0, applied.Output);
            applied.Output.Should().Contain("Técnicos escritos: 2");
            again.Output.Should().Contain("Técnicos con cambios pendientes: 0").And.Contain("Técnicos escritos: 0");

            var stored = await ByIdAsync(technicians, old);
            stored["nombrePublico"].AsString.Should().Be(name);
            stored["palabrasBusqueda"].AsBsonArray.Select(x => x.AsString).Should().Equal(TechnicianSearchFields.WordsOf(name, business), "el script normaliza igual que el servicio");
            stored["calificacion"].ToDouble().Should().Be(4.5);
            stored["totalResenas"].ToInt32().Should().Be(2);
            stored["coloniaBaseNombre"].AsString.Should().Be("Colonia Ficticia");
            var withoutReviews = await ByIdAsync(technicians, hidden);
            withoutReviews.Contains("calificacion").Should().BeFalse();
            withoutReviews["totalResenas"].ToInt32().Should().Be(0);
            (await ByIdAsync(technicians, removed)).Contains("palabrasBusqueda").Should().BeFalse("una cuenta eliminada no vuelve a ser buscable");
        }
        finally
        {
            await client.DropDatabaseAsync(database);
        }
    }

    [Fact]
    public void El_Relleno_No_Esta_Entre_Los_Scripts_Que_El_Despliegue_Aplica_Solo_CA_027_04()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Knock.slnx")))
        {
            root = root.Parent;
        }

        var topLevel = Directory.GetFiles(Path.Combine(root!.FullName, "db"), "*.js").Select(Path.GetFileName);

        topLevel.Should().NotContain("tecnicos-busqueda.js", "db/entrega.js y el bucle de despliegue aplican solo los numerados del primer nivel de db/");
        File.Exists(Path.Combine(root.FullName, "db", "relleno", "tecnicos-busqueda.js")).Should().BeTrue();
    }

    private static async Task<List<string>> AllAsync(IMongoCollection<BsonDocument> collection) =>
        [.. (await collection.Find(FilterDefinition<BsonDocument>.Empty).SortBy(d => d["_id"]).ToListAsync()).Select(d => d.ToJson())];

    private static Task<BsonDocument> ByIdAsync(IMongoCollection<BsonDocument> collection, ObjectId id) =>
        collection.Find(new BsonDocument("_id", id)).SingleAsync();
}
