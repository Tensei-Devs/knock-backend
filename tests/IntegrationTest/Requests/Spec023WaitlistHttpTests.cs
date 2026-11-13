using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

public sealed class Spec023WaitlistHttpTests
{
    [Fact]
    public async Task Usuario_Del_Token_Y_Limite_CA_023_10_CA_023_11_CA_023_16()
    {
        using var factory = await MarketApiFactory.CreateAsync();
        var collection = factory.Database.GetCollection<BsonDocument>("listas_espera");
        await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("usuarioId").Ascending("cp"), new CreateIndexOptions { Unique = true }));
        using var client = factory.CreateClient();
        var route = new Uri("/api/v1/zonas/lista-espera", UriKind.Relative);
        using var anonymous = await client.PostAsJsonAsync(route, new { cp = "91000" });
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var subject = Guid.NewGuid().ToString("D");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GatewayTestSupport.IssueToken(sub: subject, key: Convert.FromBase64String(MarketApiFactory.SigningKeyBase64)));
        using var content = new StringContent("{\"cp\":\"91000\",\"extra\":\"" + new string('a', 2048) + "\"}", Encoding.UTF8, "application/json");
        using var oversized = await client.PostAsync(route, content);
        oversized.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(0);

        using var first = await client.PostAsJsonAsync(route, new { cp = "91000", municipio = "Tula", usuarioId = "other", lat = 19 });
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var document = await collection.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        document["usuarioId"].AsString.Should().Be(subject);
        document.Names.Should().BeEquivalentTo("_id", "usuarioId", "cp", "municipio", "creadoEn");
        using var second = await client.PostAsJsonAsync(route, new { cp = "91000", municipio = "Other" });
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("yaRegistrado").GetBoolean().Should().BeTrue();
        (await collection.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync())["municipio"].AsString.Should().Be("Tula");
        using var fourth = await client.PostAsJsonAsync(route, new { cp = "91001" });
        using var fifth = await client.PostAsJsonAsync(route, new { cp = "91002" });
        using var sixth = await client.PostAsJsonAsync(route, new { cp = "91003" });
        sixth.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        sixth.Headers.RetryAfter.Should().NotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GatewayTestSupport.IssueToken(key: Convert.FromBase64String(MarketApiFactory.SigningKeyBase64)));
        using var otherPerson = await client.PostAsJsonAsync(route, new { cp = "91000" });
        otherPerson.StatusCode.Should().Be(HttpStatusCode.Created);
        otherPerson.Headers.CacheControl!.Private.Should().BeTrue();
        otherPerson.Headers.CacheControl.NoStore.Should().BeTrue();
    }
}
