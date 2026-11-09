using AwesomeAssertions;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Catalog.Data;
using Knock.Market.Api.Modules.Catalog.Domain;
using Knock.UnitTest.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.UnitTest.Catalog;

public sealed class CatalogStoreMaxTimeTests
{
    [Fact]
    public void EveryCatalogQueryCarriesTheConfiguredMaxTime_TC_TT_BD_06_13()
    {
        // El cliente no se conecta: las consultas solo se arman, nunca se ejecutan.
        using var client = new MongoClient(MongoTestServices.SyntheticConnectionString());
        var store = new CatalogStore(client.GetDatabase("market"), Options.Create(new KnockMongoOptions { QueryMaxTimeSeconds = 3 }), new FakeCatalogCache());
        var position = new GeoPosition(-99.3495, 20.0505);

        var queries = new[]
        {
            store.ActiveOficiosQuery(),
            store.IntersectingQuery(position),
            store.NearestCenterQuery(position, 2000),
            store.NamesByPostalCodeQuery("42803"),
            store.ColoniaExistsQuery(ObjectId.GenerateNewId(), "42803"),
        };

        queries.Should().OnlyContain(query => query.Options.MaxTime == TimeSpan.FromSeconds(3), "cada consulta lleva maxTimeMS");
    }

    [Fact]
    public void CatalogQueriesUseTheDefaultMaxTimeWhenNotConfigured_TC_TT_BD_06_13()
    {
        using var client = new MongoClient(MongoTestServices.SyntheticConnectionString());
        var store = new CatalogStore(client.GetDatabase("market"), Options.Create(new KnockMongoOptions()), new FakeCatalogCache());

        store.NamesByPostalCodeQuery("42803").Options.MaxTime.Should().Be(TimeSpan.FromSeconds(5));
    }

    /// <summary>Doble que siempre falla: estas pruebas solo arman consultas y nunca ejecutan, así que la caché no interviene.</summary>
    private sealed class FakeCatalogCache : ICatalogCache
    {
        public Task<IReadOnlyList<OficioListing>?> GetOficiosAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OficioListing>?>(null);

        public Task RemoveOficiosAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<string>?> GetNeighborhoodNamesAsync(string postalCode, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>?>(null);

        public Task RemoveNeighborhoodNamesAsync(string postalCode, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool?> GetColoniaExistsAsync(ObjectId id, string postalCode, CancellationToken cancellationToken) =>
            Task.FromResult<bool?>(null);



        public Task RemoveColoniaExistsAsync(ObjectId id, string postalCode, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<long> GetOficiosGenerationAsync(CancellationToken cancellationToken) => Task.FromResult(0L);

        public Task<bool> SetOficiosIfGenerationAsync(IReadOnlyList<OficioListing> oficios, long expectedGeneration, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<long> GetNeighborhoodNamesGenerationAsync(string postalCode, CancellationToken cancellationToken) => Task.FromResult(0L);

        public Task<bool> SetNeighborhoodNamesIfGenerationAsync(string postalCode, IReadOnlyList<string> names, long expectedGeneration, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<long> GetColoniaExistsGenerationAsync(ObjectId id, string postalCode, CancellationToken cancellationToken) => Task.FromResult(0L);

        public Task<bool> SetColoniaExistsIfGenerationAsync(ObjectId id, string postalCode, bool exists, long expectedGeneration, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
