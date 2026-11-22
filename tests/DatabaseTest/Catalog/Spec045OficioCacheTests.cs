using AwesomeAssertions;
using Knock.DatabaseTest.Support;
using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Catalog.Data;
using Knock.Market.Api.Modules.Catalog.Dtos;
using Knock.Market.Api.Modules.Catalog.Services;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.DatabaseTest.Catalog;

/// <summary>
/// Spec 045 con MongoDB y Redis reales: el detalle de un oficio sale de la caché de 24 horas de oficios activos (R-04, CA-04), una edición del panel la invalida y la siguiente consulta ya ve el cambio
/// (R-19, CA-34), y los oficios pedidos se agrupan por texto normalizado sin duplicar al mismo técnico (R-21, CA-36 y CA-41). Solo CI (Docker). Solo datos ficticios.
/// </summary>
[Collection("CatalogCacheSerial")]
public sealed class Spec045OficioCacheTests
{
    private sealed class RecordingAudit : IPanelAuditTrail
    {
        public Task<bool> RecordAsync(PanelAuditEntry entry, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private static TradeAdminService AdminService(CatalogDatabase database) =>
        new(database.Get<ITradeAdminStore>(), database.Get<CatalogStore>(), new RecordingAudit(), TimeProvider.System);

    [Fact]
    public async Task RepeatedDetailRead_QueriesMongoOnce_CA_045_04()
    {
        await using var database = await CatalogDatabase.CreateAsync(seeded: true, useRedisCache: true);
        var queries = new OficioQueries(database.Get<CatalogStore>());

        database.ClearCommands();
        var first = await queries.GetActiveAsync("ele", CancellationToken.None);
        database.FindCommands.Should().HaveCount(1, "la primera lectura es un fallo de caché");
        database.ClearCommands();
        var second = await queries.GetActiveAsync("ele", CancellationToken.None);

        database.FindCommands.Should().BeEmpty("la segunda consulta sale de la caché y la base no recibe ninguna");
        second.Should().BeEquivalentTo(first);
    }

    [Fact]
    public async Task InactiveTrade_IsNotFound_AndAnEditShowsOnTheNextRead_CA_045_34()
    {
        await using var database = await CatalogDatabase.CreateAsync(seeded: true, useRedisCache: true);
        var queries = new OficioQueries(database.Get<CatalogStore>());
        var service = AdminService(database);
        await queries.GetActiveAsync("ele", CancellationToken.None);

        await service.UpdateAsync("panel-1", "ele", new UpdateTradeBody(null, "Electricidad nueva", null, ["Corto circuito"], null, null, null, null, null), CancellationToken.None);

        var edited = await queries.GetActiveAsync("ele", CancellationToken.None);
        edited.Nombre.Should().Be("Electricidad nueva");
        edited.FrasesSugeridas.Should().Equal("Corto circuito");

        await service.UpdateAsync("panel-1", "ele", new UpdateTradeBody(null, null, null, null, null, null, null, null, false), CancellationToken.None);

        var act = async () => await queries.GetActiveAsync("ele", CancellationToken.None);
        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("trade_not_found");
    }

    [Fact]
    public async Task CreatedTrade_AppearsInThePublicDetail_AndTheAdminListKeepsTheInactiveOnes_CA_045_08_CA_045_34()
    {
        await using var database = await CatalogDatabase.CreateAsync(seeded: true, useRedisCache: true);
        var queries = new OficioQueries(database.Get<CatalogStore>());
        var service = AdminService(database);

        await service.CreateAsync("panel-1", new CreateTradeBody("gas", "Instalaciones de gas", "ambos", ["Huele a gas"], 25m, 10m, 200, 500, true), CancellationToken.None);
        await service.CreateAsync("panel-1", new CreateTradeBody("pausa", "Oficio en pausa", "taller", null, 20m, 5m, 100, 300, false), CancellationToken.None);

        (await queries.GetActiveAsync("gas", CancellationToken.None)).FrasesSugeridas.Should().Equal("Huele a gas");
        var listed = await service.ListAsync(CancellationToken.None);
        listed.Items.Should().Contain(i => i.Clave == "pausa" && !i.Activo);
        var stored = await database.Oficios.Find(Builders<BsonDocument>.Filter.Eq("clave", "gas")).FirstAsync();
        stored["nombreBusqueda"].AsString.Should().Be("INSTALACIONES DE GAS");
        stored["version"].ToInt32().Should().Be(1);
        stored["topePorcentaje"].ToDecimal().Should().Be(25m);
    }

    [Fact]
    public async Task CreateWithARepeatedKey_ThrowsConflict_AndKeepsTheFirstTrade_CA_045_13_CA_045_41()
    {
        await using var database = await CatalogDatabase.CreateAsync(seeded: true, useRedisCache: true);
        await database.Get<CatalogStore>().EnsureIndexesAsync(CancellationToken.None);
        var service = AdminService(database);
        var body = new CreateTradeBody("gas", "Gas", "ambos", null, 25m, 10m, 200, 500, true);
        await service.CreateAsync("panel-1", body, CancellationToken.None);

        var act = async () => await service.CreateAsync("panel-1", body with { Nombre = "Otro nombre" }, CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("trade_key_taken");
        (await database.Oficios.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("clave", "gas"))).Should().Be(1);
    }

    [Fact]
    public async Task TradeRequests_OfTwoTechniciansWithTheSameText_AreOneGroupWithCountTwo_AndTheSamePersonDoesNotRepeat_CA_045_36_CA_045_41()
    {
        await using var database = await CatalogDatabase.CreateAsync(seeded: true);
        var requests = database.Get<ITradeRequestStore>();
        var admin = database.Get<CatalogAdminStore>();
        var now = DateTime.UtcNow;

        var first = await requests.SaveAsync("tecnico-1", "Plomero", "plomero", now, CancellationToken.None);
        var repeated = await requests.SaveAsync("tecnico-1", "plomero ", "plomero", now, CancellationToken.None);
        await requests.SaveAsync("tecnico-2", "Plómero", "plomero", now, CancellationToken.None);
        await requests.SaveAsync("tecnico-3", "Electricista de autos", "electricista de autos", now, CancellationToken.None);

        repeated.Should().Be(first, "el mismo técnico no repite el mismo texto");
        var groups = await admin.ListTradeRequestsAsync(null, 10, CancellationToken.None);
        groups.Should().Equal(("plomero", 2L), ("electricista de autos", 1L));
    }
}
