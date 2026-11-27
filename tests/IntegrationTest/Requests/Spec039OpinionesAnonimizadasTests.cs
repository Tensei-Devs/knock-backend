using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>Spec 039, R-25 y CA-38 (con la 051, R-16 y CA-23): al eliminarse la cuenta, las opiniones que escribió esa persona quedan anonimizadas. MongoDB real; solo datos ficticios.</summary>
public sealed class Spec039OpinionesAnonimizadasTests
{
    private static readonly Dictionary<string, string?> _settings = new() { ["Security:InternalTokens:IdentityKey"] = InternalServiceTokens.KeyBase64 };

    [Fact]
    public async Task Eliminar_La_Cuenta_Anonimiza_Solo_Las_Opiniones_De_Esa_Persona_CA_039_38()
    {
        await using var host = await Spec020Host.StartAsync(_settings);
        var person = Guid.NewGuid().ToString("D");
        var mine = await SeedReviewAsync(host, person);
        var other = await SeedReviewAsync(host, Guid.NewGuid().ToString("D"));

        using var first = await DeletedAsync(host, person);
        using var again = await DeletedAsync(host, person);

        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        again.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var review = await host.OneAsync("opiniones", mine);
        review["anonimizada"].AsBoolean.Should().BeTrue();
        review["calificacion"].AsInt32.Should().Be(5, "la calificación sigue contando en la reputación");
        (await host.OneAsync("opiniones", other))["anonimizada"].AsBoolean.Should().BeFalse();
    }

    private static async Task<ObjectId> SeedReviewAsync(Spec020Host host, string client)
    {
        var id = ObjectId.GenerateNewId();
        await host.Database.GetCollection<BsonDocument>("opiniones").InsertOneAsync(new BsonDocument
        {
            { "_id", id }, { "visitaId", ObjectId.GenerateNewId() }, { "tecnicoId", ObjectId.GenerateNewId() }, { "clienteId", client }, { "calificacion", 5 },
            { "comentario", "Comentario ficticio" }, { "fotos", new BsonArray() }, { "moderacion", "aprobada" }, { "oculta", false }, { "anonimizada", false }, { "creadoEn", DateTime.UtcNow },
        });
        return id;
    }

    private static async Task<HttpResponseMessage> DeletedAsync(Spec020Host host, string person)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/v1/interno/cuentas/{person}/eliminada", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", InternalServiceTokens.Issue(person, "sin-elemento", ["cuenta"]));
        return await host.Client.SendAsync(request);
    }
}
