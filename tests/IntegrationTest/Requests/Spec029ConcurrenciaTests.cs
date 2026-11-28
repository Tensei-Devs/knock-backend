extern alias MarketApi;

using System.Net;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Common.TareasProgramadas;
using MarketApi::Knock.Market.Api.Modules.Requests.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>Spec 029, T-07: concurrencia (cancelar contra elegir; tarea y barrido a la vez) y registros sin datos personales (MongoDB real; solo corren en CI).</summary>
public sealed class Spec029ConcurrenciaTests
{
    private const int _rounds = 6;

    private static async Task<string> CodeOfAsync(HttpResponseMessage response) =>
        (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString()!;

    [Fact]
    public async Task Cancelar_Contra_Elegir_CA_029_05()
    {
        await using var host = await Spec020Host.StartAsync();
        var chosenWins = 0;
        var cancelWins = 0;
        for (var round = 0; round < _rounds; round++)
        {
            var (requestId, quotes, _) = await host.QuotedRequestAsync(2);

            var cancel = host.SendAsync(HttpMethod.Post, $"/api/v1/solicitudes/{requestId}/cancelar", host.ClientSub);
            var choose = host.ChooseAsync(requestId, quotes[0]);
            using var cancelResponse = await cancel;
            using var chooseResponse = await choose;

            var state = (await host.OneAsync("solicitudes", requestId))["estado"].AsString;
            if (chooseResponse.StatusCode == HttpStatusCode.OK)
            {
                chosenWins++;
                cancelResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
                (await CodeOfAsync(cancelResponse)).Should().Be("request_with_appointment");
                state.Should().Be("cita_confirmada");
                (await host.CountAsync("visitas", Builders<BsonDocument>.Filter.Eq("solicitudId", requestId))).Should().Be(1);
            }
            else
            {
                cancelWins++;
                cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);
                chooseResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
                (await CodeOfAsync(chooseResponse)).Should().Be("request_cancelled");
                state.Should().Be("cancelada");
                (await host.CountAsync("visitas", Builders<BsonDocument>.Filter.Eq("solicitudId", requestId))).Should().Be(0);
            }
        }

        (chosenWins + cancelWins).Should().Be(_rounds, "en cada ronda gana exactamente una de las dos operaciones");
    }

    [Fact]
    public async Task Vence_Una_Sola_Vez_Con_Tarea_Y_Barrido_A_La_Vez_CA_029_08()
    {
        await using var host = await Spec020Host.StartAsync();
        using var created = await host.CreateAsync(host.ValidBody());
        var id = ObjectId.Parse((await Spec020Host.ReadAsync(created)).GetProperty("id").GetString());
        await host.Database.GetCollection<BsonDocument>("solicitudes")
            .UpdateOneAsync(new BsonDocument("_id", id), new BsonDocument("$set", new BsonDocument("venceEn", DateTime.UtcNow.AddHours(-1))));
        var expiry = new TareaReservada(ObjectId.GenerateNewId(), "VencerSolicitud", id.ToString(), "x", 0);
        var sweep = new TareaReservada(ObjectId.GenerateNewId(), "BarrerVencidas", "recurrente:inicial", "x", 0);

        await Task.WhenAll(
            Enumerable.Range(0, 2).Select(async _ =>
            {
                using var scope = host.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<VencerSolicitud>().HandleAsync(expiry, CancellationToken.None);
            })
            .Concat(Enumerable.Range(0, 2).Select(async _ =>
            {
                using var scope = host.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<BarrerVencidas>().HandleAsync(sweep, CancellationToken.None);
            })));

        (await host.OneAsync("solicitudes", id))["estado"].AsString.Should().Be("vencida");
        (await host.CountAsync("outbox", Builders<BsonDocument>.Filter.Eq("_id", "SolicitudVencida:" + id))).Should().Be(1);
    }

    [Fact]
    public async Task Sin_Datos_Personales_CA_029_15()
    {
        using var logs = new CapturingLoggerProvider();
        await using var host = await Spec020Host.StartAsync(configure: services => services.AddLogging(logging => logging.AddProvider(logs)));
        var cancelled = await host.CreateRequestAsync();
        var expired = await host.CreateRequestAsync();
        var technician = await host.SeedPublicTechnicianAsync();
        await host.SeedQuoteAsync(cancelled, technician);
        await host.SeedQuoteAsync(expired, technician);
        using var cancelResponse = await host.SendAsync(HttpMethod.Post, $"/api/v1/solicitudes/{cancelled}/cancelar", host.ClientSub, new { motivo = "other" });
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        await host.Database.GetCollection<BsonDocument>("solicitudes")
            .UpdateOneAsync(new BsonDocument("_id", expired), new BsonDocument("$set", new BsonDocument("venceEn", DateTime.UtcNow.AddHours(-1))));
        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<VencerSolicitud>()
                .HandleAsync(new TareaReservada(ObjectId.GenerateNewId(), "VencerSolicitud", expired.ToString(), "x", 0), CancellationToken.None);
        }

        var outbox = host.Database.GetCollection<BsonDocument>("outbox");
        var cancelledData = (await outbox.Find(new BsonDocument("_id", "SolicitudCancelada:" + cancelled)).SingleAsync())["datos"].AsBsonDocument;
        var expiredData = (await outbox.Find(new BsonDocument("_id", "SolicitudVencida:" + expired)).SingleAsync())["datos"].AsBsonDocument;

        cancelledData.Names.Should().BeEquivalentTo("solicitudId", "tecnicosAvisar");
        expiredData.Names.Should().BeEquivalentTo("solicitudId", "clienteId", "tecnicosAvisar");
        var written = logs.AllMessages();
        foreach (var personal in new[] { Spec020Host.Description, Spec020Host.Address, host.ClientSub, Spec021Host.TechnicianName })
        {
            written.Should().NotContain(personal, "los registros de cancelar y vencer no llevan datos personales");
        }
    }
}
