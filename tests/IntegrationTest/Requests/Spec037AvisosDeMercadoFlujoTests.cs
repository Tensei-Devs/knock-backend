extern alias MarketApi;

using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using AwesomeAssertions;
using MarketApi::Knock.Market.Api.Common.TareasProgramadas;
using MarketApi::Knock.Market.Api.Modules.Requests.Services;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Bandeja = MarketApi::Knock.Market.Api.Common.Bandeja;

namespace Knock.IntegrationTest.Requests;

/// <summary>
/// Spec 037 (R-11) y spec 054 (R-03): lo que Mercado escribe al repartir, cotizar, elegir, cancelar y vencer llega a <c>interno/avisos</c> con la persona y el recurso leídos de sus propios documentos.
/// MongoDB real; el destino es un manejador HTTP de mentira; solo datos ficticios.
/// </summary>
public sealed class Spec037AvisosDeMercadoFlujoTests
{
    [Fact]
    public async Task Los_Eventos_De_Una_Solicitud_Se_Entregan_Como_Avisos_CA_054_05()
    {
        using var handler = new Spec053DespachadorBandejaTests.CaptureHandler(HttpStatusCode.Accepted);
        await using var host = await Spec020Host.StartAsync(
            new Dictionary<string, string?>
            {
                ["Oleadas:Habilitado"] = "True",
                ["TareasProgramadas:IntervaloSegundos"] = "3600",
                ["Services:Messaging:BaseUrl"] = "http://messaging.test",
                ["Services:Messaging:InternalKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            },
            services => services.AddHttpClient(Bandeja.EntregaOpinionModerada.MessagingClientName).ConfigurePrimaryHttpMessageHandler(() => handler));
        var technician = await host.SeedPublicTechnicianAsync();
        await host.Database.GetCollection<BsonDocument>("tecnicos").UpdateOneAsync(
            new BsonDocument("_id", technician.Id),
            new BsonDocument("$set", new BsonDocument { { "disponibleHasta", DateTime.UtcNow.AddDays(1) }, { "calificacion", 5 }, { "trabajosTerminados", 5 } }));
        var chosen = await host.CreateRequestAsync();
        var cancelled = await host.CreateRequestAsync();
        var expired = await host.CreateRequestAsync();
        await RunTasksAsync(host);

        await QuoteAsync(host, chosen, technician.Sub);
        await QuoteAsync(host, cancelled, technician.Sub);
        var quote = (await host.Database.GetCollection<BsonDocument>("cotizaciones").Find(new BsonDocument("solicitudId", chosen)).SingleAsync())["_id"].AsObjectId;
        using var choice = await host.ChooseAsync(chosen, quote);
        choice.StatusCode.Should().Be(HttpStatusCode.OK);
        var visit = (await Spec020Host.ReadAsync(choice)).GetProperty("visitaId").GetString()!;
        using var cancel = await host.SendAsync(HttpMethod.Post, $"/api/v1/solicitudes/{cancelled}/cancelar", host.ClientSub);
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        await host.SetRequestAsync(expired, "enviada", DateTime.UtcNow.AddHours(-1));
        await using (var scope = host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<VencerSolicitud>()
                .HandleAsync(new TareaReservada(ObjectId.GenerateNewId(), "VencerSolicitud", expired.ToString(), "x", 0), CancellationToken.None);
        }

        await DispatchAllAsync(host);

        var notices = handler.Requests.Where(x => x.Path == "/api/v1/interno/avisos").Select(x => Notice(x.Body)).ToList();
        var client = host.ClientSub;
        var sub = technician.Sub;
        (string, string, string, string)[] expected =
        [
            ("solicitud_nueva", sub, "tecnico", chosen.ToString()),
            ("solicitud_nueva", sub, "tecnico", cancelled.ToString()),
            ("solicitud_nueva", sub, "tecnico", expired.ToString()),
            ("cotizacion_nueva", client, "cliente", chosen.ToString()),
            ("cotizacion_nueva", client, "cliente", cancelled.ToString()),
            ("cotizacion_aceptada", sub, "tecnico", visit),
            ("solicitud_cancelada", sub, "tecnico", cancelled.ToString()),
            ("solicitud_vencida", client, "cliente", expired.ToString()),
        ];
        notices.Should().BeEquivalentTo(expected);
        var types = Bandeja.AvisosDeSolicitud.All.Select(x => x.EventType);
        var messages = await host.Database.GetCollection<BsonDocument>("outbox").Find(Builders<BsonDocument>.Filter.In("tipo", types)).ToListAsync();
        messages.Should().OnlyContain(x => x["estado"] == "entregado");
    }

    private static async Task QuoteAsync(Spec020Host host, ObjectId requestId, string sub)
    {
        using var response = await host.SendAsync(HttpMethod.Post, $"/api/v1/solicitudes/{requestId}/cotizaciones", sub,
            new { tipoPrecio = "fijo", precio = 100, dia = Spec020Host.Tomorrow(), hora = "12:00" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static async Task RunTasksAsync(Spec020Host host)
    {
        for (var i = 0; i < 10; i++)
        {
            await using var scope = host.Services.CreateAsyncScope();
            if (!await scope.ServiceProvider.GetRequiredService<EjecutorDeTareasProgramadas>().ExecuteOneAsync(CancellationToken.None))
            {
                return;
            }
        }
    }

    private static async Task DispatchAllAsync(Spec020Host host)
    {
        for (var i = 0; i < 50; i++)
        {
            await using var scope = host.Services.CreateAsyncScope();
            if (!await scope.ServiceProvider.GetRequiredService<Bandeja.DespachadorDeBandeja>().DispatchOneAsync(CancellationToken.None))
            {
                return;
            }
        }
    }

    private static (string, string, string, string) Notice(string body)
    {
        var json = JsonElement.Parse(body);
        return (json.GetProperty("tipo").GetString()!, json.GetProperty("destinatarioId").GetString()!, json.GetProperty("rol").GetString()!, json.GetProperty("recurso").GetString()!);
    }
}
