using System.Net;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.SecurityTest.Market;

/// <summary>Spec 042: sin credencial válida no se accede, y nadie quita a los técnicos guardados por otra persona (R-12). Solo datos ficticios.</summary>
public class Spec042ConfianzaSecurityTests
{
    private const string _route = "/api/v1/me/tecnicos-confianza";

    [Theory]
    [InlineData("GET", "")]
    [InlineData("PUT", "/665f1c2a9b3e4d0012345678")]
    [InlineData("DELETE", "/665f1c2a9b3e4d0012345678")]
    public async Task Sin_Token_O_Vencido_Responde_401_CA_042_05(string method, string suffix)
    {
        var hosts = await SecurityHosts.SharedAsync();

        using var none = await SecureHttp.SendAsync(hosts.MarketClient, new HttpMethod(method), _route + suffix);
        using var expired = await SecureHttp.SendAsync(hosts.MarketClient, new HttpMethod(method), _route + suffix, bearer: TokenForge.Expired());

        await SecureHttp.AssertProblemAsync(none, HttpStatusCode.Unauthorized);
        await SecureHttp.AssertProblemAsync(expired, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task No_Se_Quita_El_Tecnico_Guardado_Por_Otra_Persona_CA_042_16()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var owner = TokenForge.Sub();
        var technician = ObjectId.GenerateNewId();
        var saved = hosts.Market.Services.GetRequiredKeyedService<IMongoDatabase>("Trust").GetCollection<BsonDocument>("tecnicos_confianza");
        await saved.InsertOneAsync(new BsonDocument { ["clienteId"] = owner, ["tecnicoId"] = technician, ["guardadoEn"] = DateTime.UtcNow });
        var filter = Builders<BsonDocument>.Filter.Eq("clienteId", owner);

        using var foreign = await SecureHttp.SendAsync(hosts.MarketClient, HttpMethod.Delete, $"{_route}/{technician}", bearer: TokenForge.Valid(TokenForge.Sub()));

        await SecureHttp.AssertProblemAsync(foreign, HttpStatusCode.NotFound, owner);
        (await saved.CountDocumentsAsync(filter)).Should().Be(1);

        // Control: quien lo guardó sí puede quitarlo.
        using var own = await SecureHttp.SendAsync(hosts.MarketClient, HttpMethod.Delete, $"{_route}/{technician}", bearer: TokenForge.Valid(owner));
        own.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await saved.CountDocumentsAsync(filter)).Should().Be(0);
    }
}
