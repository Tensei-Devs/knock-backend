extern alias MarketApi;

using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.Hosting.Data;
using Knock.IntegrationTest.Support;
using Knock.SecurityTest.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Bandeja = MarketApi::Knock.Market.Api.Common.Bandeja;
using Datos = MarketApi::Knock.Market.Api.Modules.Requests.Data;

namespace Knock.SecurityTest.Messaging;

/// <summary>
/// Spec 037 (R-11 y R-14) y spec 054 (R-03), de punta a punta: un evento del buzón de Mercado sale por su despachador, pasa por <c>POST /api/v1/interno/avisos</c> real y la persona lo ve en
/// <c>GET /api/v1/avisos</c>; nadie más. MongoDB real; solo datos ficticios.
/// </summary>
public sealed class Spec037AvisoDeMercadoAPersonaTests
{
    [Fact]
    public async Task CotizacionEnviada_LlegaALaBandejaDeAvisosDeLaClienta_CA_054_05()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var messagingName = "s037_punta_msg_" + suffix;
        var marketName = "s037_punta_mkt_" + suffix;
        var marketKey = RandomNumberGenerator.GetBytes(32);
        using var mongo = new MongoClient(fixture.ConnectionString);
        try
        {
            await using var messaging = await Spec037InternoAvisosSeguridadTests.CreateHostAsync(fixture.ConnectionString, messagingName, marketKey);
            using var client = messaging.CreateClient();
            var market = mongo.GetDatabase(marketName);
            var person = Guid.NewGuid().ToString("D");
            var request = ObjectId.GenerateNewId();
            var quote = ObjectId.GenerateNewId().ToString();
            await market.GetCollection<BsonDocument>("solicitudes").InsertOneAsync(new BsonDocument { { "_id", request }, { "clienteId", person } });
            await market.GetCollection<BsonDocument>("outbox").InsertOneAsync(new BsonDocument
            {
                { "_id", "CotizacionEnviada:" + quote },
                { "tipo", "CotizacionEnviada" },
                { "datos", new BsonDocument { { "solicitudId", request.ToString() }, { "cotizacionId", quote } } },
                { "estado", "pendiente" },
                { "intentos", 0 },
                { "creadoEn", DateTime.UtcNow },
            });
            using var factory = new ServerClients(messaging.Server.CreateHandler());
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Services:Messaging:BaseUrl"] = "http://localhost",
                ["Services:Messaging:InternalKey"] = Convert.ToBase64String(marketKey),
            }).Build();
            var delivery = new Bandeja.EntregaAvisoDeSolicitud(
                Bandeja.AvisosDeSolicitud.All.Single(x => x.EventType == "CotizacionEnviada"),
                new Datos.DestinatariosDeAvisoStore(market, Options.Create(new KnockMongoOptions())),
                factory,
                configuration,
                Options.Create(new Bandeja.BandejaOptions()),
                TimeProvider.System);
            var dispatcher = new Bandeja.DespachadorDeBandeja(new Datos.BandejaStore(market), [delivery], TimeProvider.System, NullLogger<Bandeja.DespachadorDeBandeja>.Instance);

            (await dispatcher.DispatchOneAsync(CancellationToken.None)).Should().BeTrue();
            using var list = await SecureHttp.SendAsync(client, HttpMethod.Get, "/api/v1/avisos", bearer: TokenForge.Valid(person));
            using var stranger = await SecureHttp.SendAsync(client, HttpMethod.Get, "/api/v1/avisos", bearer: TokenForge.Valid(Guid.NewGuid().ToString("D")));

            var stored = await market.GetCollection<BsonDocument>("outbox").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
            stored["estado"].AsString.Should().Be("entregado");
            System.Text.Json.JsonElement[] notices = [.. (await SecureHttp.ReadJsonAsync(list)).GetProperty("items").EnumerateArray()];
            var notice = notices.Should().ContainSingle().Subject;
            notice.GetProperty("titulo").GetString().Should().Be("Nueva cotización");
            notice.GetProperty("comoRol").GetString().Should().Be("cliente");
            notice.GetProperty("destino").GetProperty("id").GetString().Should().Be(request.ToString());
            (await SecureHttp.ReadJsonAsync(stranger)).GetProperty("items").GetArrayLength().Should().Be(0);
        }
        finally
        {
            await mongo.DropDatabaseAsync(messagingName);
            await mongo.DropDatabaseAsync(marketName);
        }
    }

    private sealed class ServerClients(HttpMessageHandler handler) : IHttpClientFactory, IDisposable
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);

        public void Dispose() => handler.Dispose();
    }
}
