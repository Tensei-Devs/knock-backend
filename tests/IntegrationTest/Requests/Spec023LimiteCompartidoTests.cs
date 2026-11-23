using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>Spec 023 T-04: el límite de 5 registros por hora por persona se comparte entre réplicas (contador en Redis real).</summary>
public sealed class Spec023LimiteCompartidoTests
{
    [Fact]
    public async Task Limite_De_Lista_De_Espera_Se_Comparte_Entre_Replicas_CA_023_11()
    {
        // El host de pruebas lee la cadena de Redis después de registrar los servicios, así que sin esta conexión cada réplica limitaría por su cuenta.
        var redis = await RedisTestFixture.SharedAsync();
        using var connection = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        using var replicaA = await CreateReplicaAsync(connection);
        using var replicaB = await CreateReplicaAsync(connection);
        var subject = Guid.NewGuid().ToString("D");
        using var clientA = WaitlistHttp.SignedIn(replicaA, subject);
        using var clientB = WaitlistHttp.SignedIn(replicaB, subject);

        for (var i = 0; i < 3; i++)
        {
            using var accepted = await clientA.PostAsJsonAsync(WaitlistHttp.Route, new { cp = $"9200{i}" });
            accepted.StatusCode.Should().Be(HttpStatusCode.Created);
        }
        for (var i = 3; i < 5; i++)
        {
            using var accepted = await clientB.PostAsJsonAsync(WaitlistHttp.Route, new { cp = $"9200{i}" });
            accepted.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        using var limitedA = await clientA.PostAsJsonAsync(WaitlistHttp.Route, new { cp = "92009" });
        using var limitedB = await clientB.PostAsJsonAsync(WaitlistHttp.Route, new { cp = "92009" });
        limitedA.StatusCode.Should().Be(HttpStatusCode.TooManyRequests, "las 5 peticiones se contaron entre las dos réplicas");
        limitedB.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limitedB.Headers.RetryAfter.Should().NotBeNull();

        using var otherPerson = WaitlistHttp.SignedIn(replicaB);
        using var other = await otherPerson.PostAsJsonAsync(WaitlistHttp.Route, new { cp = "92009" });
        other.StatusCode.Should().Be(HttpStatusCode.Created, "el límite es por persona");
    }

    private static async Task<MarketApiFactory> CreateReplicaAsync(IConnectionMultiplexer connection)
    {
        var factory = await MarketApiFactory.CreateAsync(configureServices: services => services.AddSingleton(connection));
        await factory.Database.GetCollection<BsonDocument>("listas_espera").Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("usuarioId").Ascending("cp"), new CreateIndexOptions { Unique = true }));
        return factory;
    }
}
