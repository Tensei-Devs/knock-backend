extern alias MarketApi;

using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Bandeja = MarketApi::Knock.Market.Api.Common.Bandeja;
using BandejaStore = MarketApi::Knock.Market.Api.Modules.Requests.Data.BandejaStore;
using TareasProgramadasStore = MarketApi::Knock.Market.Api.Modules.Requests.Data.TareasProgramadasStore;

namespace Knock.IntegrationTest.Requests;

/// <summary>
/// Spec 054 en Mercado: al llegar a un estado terminal, el evento y la tarea llevan <c>limpiarEn</c> (R-11: 14 días lo terminado y 90 lo fallido; lo borra el índice TTL de <c>db/054</c>) y el despachador
/// tiene registradas las entregas de R-03. MongoDB real; solo datos ficticios.
/// </summary>
public sealed class Spec054MercadoLimpiezaTests
{
    private static readonly DateTime _now = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Evento_Entregado_O_Fallido_Lleva_Su_Fecha_De_Limpieza_CA_054_18()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        using var client = new MongoClient(fixture.ConnectionString);
        var databaseName = NewDatabaseName();
        var database = client.GetDatabase(databaseName);
        var outbox = database.GetCollection<BsonDocument>("outbox");
        await outbox.InsertManyAsync(Enumerable.Range(0, 3).Select(i => new BsonDocument
        {
            { "_id", $"evento-{i}" }, { "tipo", "X" }, { "datos", new BsonDocument() }, { "estado", "pendiente" }, { "intentos", 0 }, { "creadoEn", _now.AddMinutes(i - 10) },
        }));
        var store = new BandejaStore(database);

        var delivered = await store.ReserveAsync(_now, ["X"], CancellationToken.None);
        (await store.MarkDeliveredAsync(delivered!, _now, CancellationToken.None)).Should().BeTrue();
        var rejected = await store.ReserveAsync(_now, ["X"], CancellationToken.None);
        (await store.FailAsync(rejected!, permanent: true, _now, CancellationToken.None)).Should().BeTrue();
        var retried = await store.ReserveAsync(_now, ["X"], CancellationToken.None);
        (await store.FailAsync(retried!, permanent: false, _now, CancellationToken.None)).Should().BeTrue();

        (await FindAsync(outbox, delivered!.Id))["limpiarEn"].ToUniversalTime().Should().Be(_now.AddDays(14));
        (await FindAsync(outbox, rejected!.Id))["limpiarEn"].ToUniversalTime().Should().Be(_now.AddDays(90));
        (await FindAsync(outbox, retried!.Id)).Contains("limpiarEn").Should().BeFalse("sigue pendiente de reintento");
        await client.DropDatabaseAsync(databaseName);
    }

    [Fact]
    public async Task Tarea_Hecha_O_Fallida_Lleva_Su_Fecha_De_Limpieza_CA_054_18()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        using var client = new MongoClient(fixture.ConnectionString);
        var databaseName = NewDatabaseName();
        var database = client.GetDatabase(databaseName);
        var tasks = database.GetCollection<BsonDocument>("tareas_programadas");
        var store = new TareasProgramadasStore(database);
        await store.ScheduleByKeyAsync(null, "X", "hecha", _now.AddMinutes(-3), _now, CancellationToken.None);
        var done = await store.ReserveAsync(_now, CancellationToken.None);
        (await store.CompleteAsync(done!, _now, CancellationToken.None)).Should().BeTrue();
        await store.ScheduleByKeyAsync(null, "X", "reintento", _now.AddMinutes(-2), _now, CancellationToken.None);
        var retried = await store.ReserveAsync(_now, CancellationToken.None);
        (await store.FailAsync(retried!, _now, CancellationToken.None)).Should().BeTrue();
        await store.ScheduleByKeyAsync(null, "X", "fallida", _now.AddMinutes(-1), _now, CancellationToken.None);
        var last = await store.ReserveAsync(_now, CancellationToken.None);
        (await store.FailAsync(last! with { Intentos = 4 }, _now, CancellationToken.None)).Should().BeTrue();

        (await FindAsync(tasks, done!.Id))["limpiarEn"].ToUniversalTime().Should().Be(_now.AddDays(14));
        (await FindAsync(tasks, last!.Id))["limpiarEn"].ToUniversalTime().Should().Be(_now.AddDays(90));
        (await FindAsync(tasks, retried!.Id)).Contains("limpiarEn").Should().BeFalse("sigue pendiente de reintento");
        await client.DropDatabaseAsync(databaseName);
    }

    [Fact]
    public async Task El_Despachador_De_Mercado_Registra_Las_Entregas_De_Eventos_Y_Avisos_CA_054_05()
    {
        await using var host = await Spec020Host.StartAsync();
        await using var scope = host.Services.CreateAsyncScope();

        var types = scope.ServiceProvider.GetServices<Bandeja.IEntregaDeBandeja>().Select(x => x.Tipo);

        types.Should().Contain(["CotizacionElegida", "visita_afectada"]).And.OnlyHaveUniqueItems();
    }

    private static async Task<BsonDocument> FindAsync(IMongoCollection<BsonDocument> collection, BsonValue id) =>
        await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).SingleAsync();

    private static string NewDatabaseName() => "s054_limpieza_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
}
