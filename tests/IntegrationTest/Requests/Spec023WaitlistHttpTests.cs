using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>
/// Spec 023 sobre contenedores: lo que se guarda en MongoDB. Los errores 401, 413, 422, 400 y 429 y sus encabezados se prueban sin contenedores en
/// <see cref="Spec023WaitlistProblemTests"/>.
/// </summary>
public sealed class Spec023WaitlistHttpTests
{
    private static readonly Uri _route = new("/api/v1/zonas/lista-espera", UriKind.Relative);

    private static async Task<(MarketApiFactory Factory, IMongoCollection<BsonDocument> Collection)> CreateHostAsync()
    {
        var factory = await MarketApiFactory.CreateAsync();
        var collection = factory.Database.GetCollection<BsonDocument>("listas_espera");
        await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("usuarioId").Ascending("cp"), new CreateIndexOptions { Unique = true }));
        return (factory, collection);
    }

    private static HttpClient SignedIn(MarketApiFactory factory, string subject)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GatewayTestSupport.IssueToken(sub: subject, key: Convert.FromBase64String(MarketApiFactory.SigningKeyBase64)));
        return client;
    }

    [Fact]
    public async Task FirstJoin_Stores_TheSubjectOfTheToken_AndOnlyTheAllowedFields_CA_023_10()
    {
        var (factory, collection) = await CreateHostAsync();
        using var host = factory;
        var subject = Guid.NewGuid().ToString("D");
        using var client = SignedIn(factory, subject);

        using var response = await client.PostAsJsonAsync(_route, new { cp = "91000", municipio = "Tula", usuarioId = "other", lat = 19 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var document = await collection.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        document["usuarioId"].AsString.Should().Be(subject);
        document.Names.Should().BeEquivalentTo("_id", "usuarioId", "cp", "municipio", "creadoEn");
    }

    [Fact]
    public async Task RepeatedJoin_Returns200_KeepsTheFirstEntry_AndDoesNotMixPeople_CA_023_11()
    {
        var (factory, collection) = await CreateHostAsync();
        using var host = factory;
        using var client = SignedIn(factory, Guid.NewGuid().ToString("D"));
        using var first = await client.PostAsJsonAsync(_route, new { cp = "91000", municipio = "Tula" });

        using var second = await client.PostAsJsonAsync(_route, new { cp = "91000", municipio = "Other" });
        using var otherClient = SignedIn(factory, Guid.NewGuid().ToString("D"));
        using var otherPerson = await otherClient.PostAsJsonAsync(_route, new { cp = "91000" });

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("yaRegistrado").GetBoolean().Should().BeTrue();
        otherPerson.StatusCode.Should().Be(HttpStatusCode.Created);
        var stored = await collection.Find(Builders<BsonDocument>.Filter.Eq("municipio", "Tula")).ToListAsync();
        stored.Should().ContainSingle("la segunda petición no reemplaza el municipio de la primera");
        (await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(2);
    }
}
