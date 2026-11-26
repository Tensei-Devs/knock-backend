using System.Net;
using AwesomeAssertions;
using Knock.IntegrationTest.Requests;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Tecnicos;

/// <summary>
/// <c>GET /api/v1/tecnicos</c> (spec 046, TEC-02, R-06): al recorrer todas las páginas con el cursor, cada técnico sale exactamente una vez y en el orden pedido.
/// MongoDB real; los identificadores van en orden contrario al esperado para que un cursor que solo mire el <c>_id</c> se note. Solo datos ficticios.
/// </summary>
public sealed class Spec046PaginacionOrdenadaTests
{
    private const string _route = "/api/v1/tecnicos";
    private const double _lat = 20.0500;
    private const double _lng = -99.3400;

    [Theory]
    [InlineData("")]
    [InlineData("&orden=calificacion")]
    [InlineData("&orden=calificacion&lat=20.05&lng=-99.34")]
    [InlineData("&orden=calificacion&lat=20.05&lng=-99.34&radioKm=12")]
    public async Task Por_Calificacion_Cada_Tecnico_Sale_Una_Vez_Y_En_Orden_CA_046_08(string extra)
    {
        await using var host = await Spec020Host.StartAsync();
        var ids = await SeedAsync(host, [(3.0, 0), (5.0, 0), (null, 0), (4.0, 0), (5.0, 0), (1.0, 0), (null, 0)]);
        // Calificación de mayor a menor; en empate, el más reciente primero; sin calificación, al final.
        var expected = new[] { ids[4], ids[1], ids[3], ids[0], ids[5], ids[6], ids[2] };

        var seen = await WalkAsync(host, "?limite=2" + extra);

        seen.Should().Equal(expected);
    }

    [Fact]
    public async Task Con_Radio_Y_Orden_Por_Calificacion_No_Salen_Los_De_Fuera_CA_046_08()
    {
        await using var host = await Spec020Host.StartAsync();
        // 0.040 grados son unos 4.4 km; 0.200, unos 22 km: queda fuera del radio de 12 km aunque tenga la mejor calificación.
        var ids = await SeedAsync(host, [(5.0, 0.200), (3.0, 0.040), (4.0, 0.001)]);

        var seen = await WalkAsync(host, "?limite=1&orden=calificacion&lat=20.05&lng=-99.34&radioKm=12");

        seen.Should().Equal(ids[2], ids[1]);
    }

    [Theory]
    [InlineData("&lat=20.05&lng=-99.34")]
    [InlineData("&lat=20.05&lng=-99.34&radioKm=12")]
    public async Task Por_Distancia_Cada_Tecnico_Sale_Una_Vez_Y_De_Cerca_A_Lejos_CA_046_08(string position)
    {
        await using var host = await Spec020Host.StartAsync();
        // El desplazamiento aleja hacia el norte; dos pares comparten el mismo punto (técnicos de la misma colonia base).
        var ids = await SeedAsync(host, [(null, 0.040), (null, 0.001), (null, 0.020), (null, 0.001), (null, 0.060), (null, 0.020), (null, 0.000)]);

        var seen = await WalkAsync(host, "?limite=2&orden=distancia" + position);

        seen.Should().OnlyHaveUniqueItems().And.HaveCount(ids.Count);
        var offsets = new[] { 0.040, 0.001, 0.020, 0.001, 0.060, 0.020, 0.000 };
        seen.Select(id => offsets[ids.IndexOf(id)]).Should().BeInAscendingOrder();
    }

    private static async Task<List<string>> SeedAsync(Spec020Host host, (double? Rating, double Offset)[] technicians)
    {
        var collection = host.Database.GetCollection<BsonDocument>("tecnicos");
        await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Geo2DSphere("ubicacionBase")));
        var ids = new List<string>();
        foreach (var (rating, offset) in technicians)
        {
            var id = ObjectId.GenerateNewId();
            var document = new BsonDocument
            {
                ["_id"] = id,
                ["usuarioId"] = Guid.NewGuid().ToString("D"),
                ["nombre"] = "Técnico Ficticio",
                ["estado"] = "activo",
                ["oculto"] = false,
                ["oficios"] = new BsonArray { Spec020Host.Plumbing },
                ["coloniaBaseId"] = host.ColoniaId.ToString(),
                ["ubicacionBase"] = new BsonDocument { ["type"] = "Point", ["coordinates"] = new BsonArray { _lng, _lat + offset } },
            };
            if (rating is { } value)
            {
                document["calificacion"] = value;
            }

            await collection.InsertOneAsync(document);
            ids.Add(id.ToString());
        }

        return ids;
    }

    /// <summary>Sigue <c>nextCursor</c> hasta el final y devuelve los identificadores en el orden en que llegaron (con un tope para no girar sin fin).</summary>
    private static async Task<List<string>> WalkAsync(Spec020Host host, string query)
    {
        var seen = new List<string>();
        string? cursor = null;
        for (var page = 0; page < 12; page++)
        {
            var url = _route + query + (cursor is null ? string.Empty : "&cursor=" + cursor);
            using var response = await host.SendAsync(HttpMethod.Get, url);
            response.StatusCode.Should().Be(HttpStatusCode.OK, url);
            var json = await Spec020Host.ReadAsync(response);
            seen.AddRange(json.GetProperty("tecnicos").EnumerateArray().Select(t => t.GetProperty("id").GetString()!));
            cursor = json.GetProperty("nextCursor").GetString();
            if (cursor is null)
            {
                break;
            }
        }

        return seen;
    }
}
