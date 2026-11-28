extern alias IdentityApi;

using System.Net;
using AwesomeAssertions;
using IdentityApi::Knock.Identity.Api.Modules.Safety.Data;
using Knock.SecurityTest.Support;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.SecurityTest.Identity;

/// <summary>
/// Spec 043 (R-24, CA-29): solo quien bloqueó quita su bloqueo; ni la persona bloqueada ni una tercera lo levantan. Identidad real en Production con MongoDB y Redis reales.
/// Solo datos ficticios.
/// </summary>
public sealed class AccesoBloqueosSecurityTests
{
    [Fact]
    public async Task NeitherTheBlockedPersonNorAThirdOne_LiftsMyBlock_CA_043_29()
    {
        await using var factory = await SecurityIdentityFactory.CreateAsync();
        using var client = factory.CreateClient();
        await factory.Services.GetRequiredService<BloqueosStore>().EnsureIndexesAsync(CancellationToken.None);
        await MongoCollections.CreateIfMissingAsync(factory.Database, "outbox");
        var owner = await factory.SignInAsync(client);
        var blocked = await factory.SignInAsync(client);
        var third = await factory.SignInAsync(client);
        var blocks = factory.Database.GetCollection<BsonDocument>("bloqueos");
        var byOwner = Builders<BsonDocument>.Filter.Eq("bloqueaId", owner.Sub);
        var path = $"/api/v1/usuarios/{blocked.Sub}/bloqueo";
        using var created = await SecureHttp.SendAsync(client, HttpMethod.Post, path, bearer: owner.AccessToken);
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        using var byThird = await SecureHttp.SendAsync(client, HttpMethod.Delete, path, bearer: third.AccessToken);
        using var byBlocked = await SecureHttp.SendAsync(client, HttpMethod.Delete, $"/api/v1/usuarios/{owner.Sub}/bloqueo", bearer: blocked.AccessToken);

        await SecureHttp.AssertProblemAsync(byThird, HttpStatusCode.NotFound, owner.Sub);
        await SecureHttp.AssertProblemAsync(byBlocked, HttpStatusCode.NotFound, owner.Sub);
        (await blocks.CountDocumentsAsync(byOwner)).Should().Be(1, "el bloqueo sigue en pie");

        // Control: quien bloqueó sí lo quita con la misma petición.
        using var byWhoBlocked = await SecureHttp.SendAsync(client, HttpMethod.Delete, path, bearer: owner.AccessToken);

        byWhoBlocked.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await blocks.CountDocumentsAsync(byOwner)).Should().Be(0);
    }
}
