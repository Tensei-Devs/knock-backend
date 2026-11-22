using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Knock.SecurityTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.SecurityTest.Messaging;

public sealed class Spec037DeviceAvailabilityTests
{
    [Fact]
    public async Task EnabledHost_RegistersAndDeletesOnlyOwnedDevice()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        var name = "s037_http_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        using var mongo = new MongoClient(fixture.ConnectionString);
        var devices = mongo.GetDatabase(name).GetCollection<BsonDocument>("notifications.dispositivos");
        await devices.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("tokenHuella"), new CreateIndexOptions { Unique = true }));
        await devices.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("id"), new CreateIndexOptions { Unique = true }));
        try
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:KnockMongo"] = fixture.ConnectionString,
                ["Mongo:Databases:Messaging"] = name,
                ["Avisos:Devices:WritesEnabled"] = "true",
                ["Avisos:Devices:ActiveEncryptionVersion"] = "1",
                ["Avisos:Devices:EncryptionKeys:1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                ["Avisos:Devices:IndexKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            };
            await using var host = await SecurityMessagingFactory.CreateAsync(settings);
            using var client = host.CreateClient();
            var owner = TokenForge.Valid();
            var other = TokenForge.Valid(sub: Guid.NewGuid().ToString());
            const string token = "synthetic-push-value";
            using var created = await SecureHttp.PostJsonAsync(client, "/api/v1/dispositivos", new { identificador = token, plataforma = "ios" }, owner);
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            var text = await created.Content.ReadAsStringAsync();
            text.Should().NotContain(token);
            using var json = JsonDocument.Parse(text);
            var id = json.RootElement.GetProperty("id").GetString()!;
            created.Headers.Location!.ToString().Should().NotContain(token).And.EndWith(id);
            using var repeated = await SecureHttp.PostJsonAsync(client, "/api/v1/dispositivos", new { identificador = token, plataforma = "ios" }, owner);
            repeated.StatusCode.Should().Be(HttpStatusCode.OK);
            using var repeatedJson = JsonDocument.Parse(await repeated.Content.ReadAsStringAsync());
            repeatedJson.RootElement.GetProperty("id").GetString().Should().Be(id);
            using var takeover = await SecureHttp.PostJsonAsync(client, "/api/v1/dispositivos", new { identificador = token, plataforma = "android" }, other);
            takeover.StatusCode.Should().Be(HttpStatusCode.Created, "el último registro gana: el teléfono cambió de cuenta (spec 037, R-01)");
            (await takeover.Content.ReadAsStringAsync()).Should().NotContain(token);
            using var invalid = await SecureHttp.PostJsonAsync(client, "/api/v1/dispositivos", new { identificador = token, plataforma = "Windows" }, owner);
            invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await invalid.Content.ReadAsStringAsync()).Should().NotContain(token);
            using var takeoverJson = JsonDocument.Parse(await takeover.Content.ReadAsStringAsync());
            var newId = takeoverJson.RootElement.GetProperty("id").GetString()!;
            // R-02: DELETE responde 204 siempre; solo la dueña actual borra algo.
            using var staleDelete = await SecureHttp.SendAsync(client, HttpMethod.Delete, $"/api/v1/dispositivos/{id}", bearer: owner);
            using var foreignDelete = await SecureHttp.SendAsync(client, HttpMethod.Delete, $"/api/v1/dispositivos/{newId}", bearer: owner);
            staleDelete.StatusCode.Should().Be(HttpStatusCode.NoContent);
            foreignDelete.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await devices.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(1, "la persona anterior no borra el dispositivo de otra");
            using var deleted = await SecureHttp.SendAsync(client, HttpMethod.Delete, $"/api/v1/dispositivos/{newId}", bearer: other);
            using var missing = await SecureHttp.SendAsync(client, HttpMethod.Delete, $"/api/v1/dispositivos/{newId}", bearer: other);
            deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
            missing.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await devices.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(0);
        }
        finally
        {
            await mongo.DropDatabaseAsync(name);
        }
    }

    [Fact]
    public async Task MissingKeys_RequiresSessionAndKeepsHealthAvailable()
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();
        using var anonymous = await SecureHttp.PostJsonAsync(client, "/api/v1/dispositivos", new { identificador = "synthetic-token", plataforma = "ios" });
        using var authenticated = await SecureHttp.PostJsonAsync(client, "/api/v1/dispositivos", new { identificador = "synthetic-token", plataforma = "ios" }, TokenForge.Valid());
        using var health = await client.GetAsync(new Uri("/health", UriKind.Relative));

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        authenticated.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await authenticated.Content.ReadAsStringAsync()).Should().NotContain("synthetic-token");
        health.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
