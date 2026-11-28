extern alias MarketApi;

using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using AwesomeAssertions;
using MarketApi::Knock.Market.Api.Modules.Requests.Services;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using Xunit;
using Bandeja = MarketApi::Knock.Market.Api.Common.Bandeja;

namespace Knock.IntegrationTest.Requests;

/// <summary>
/// Spec 054 (enmienda del 2026-11-27): lo que Mercado escribe al reprogramar, marcar al cliente ausente y cancelar tarde llega a <c>interno/avisos</c> con la persona correcta. MongoDB real; el destino es
/// un manejador HTTP de mentira; solo datos ficticios.
/// </summary>
public sealed class Spec054AvisosDeVisitaFlujoTests
{
    [Fact]
    public async Task Los_Eventos_De_La_Visita_Se_Entregan_Como_Avisos_CA_054_23_CA_054_25()
    {
        using var handler = new Spec053DespachadorBandejaTests.CaptureHandler(HttpStatusCode.Accepted);
        await using var host = await Spec020Host.StartAsync(
            new Dictionary<string, string?>
            {
                ["TareasProgramadas:IntervaloSegundos"] = "3600",
                ["Services:Messaging:BaseUrl"] = "http://messaging.test",
                ["Services:Messaging:InternalKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            },
            services =>
            {
                services.AddSingleton<IVisitPhotos>(new FakeVisitPhotos());
                services.AddHttpClient(Bandeja.EntregaOpinionModerada.MessagingClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
            });
        var rescheduled = await host.VisitAtAsync(TimeSpan.FromDays(3));
        await ProposeAndReplyAsync(host, rescheduled, 5, accept: false);
        await ProposeAndReplyAsync(host, rescheduled, 6, accept: true);
        await host.SetAppointmentAsync(rescheduled.VisitId, DateTime.UtcNow.AddMinutes(-30));
        using var absent = await host.PostAsync(rescheduled.VisitId, "cliente-ausente", rescheduled.Technician.Sub);
        absent.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelled = await host.VisitAtAsync(TimeSpan.FromHours(3));
        using var cancel = await host.PostAsync(cancelled.VisitId, "cancelar", host.ClientSub, new { motivo = Spec050Host.Reason });
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);

        await DispatchAllAsync(host);

        var visitTypes = new HashSet<string> { "reprogramacion_propuesta", "visita_reprogramada", "reprogramacion_sin_cambio", "cliente_ausente", "visita_cancelada" };
        var notices = handler.Requests.Where(x => x.Path == "/api/v1/interno/avisos").Select(x => Notice(x.Body)).Where(x => visitTypes.Contains(x.Type)).ToList();
        var client = host.ClientSub;
        var sub = rescheduled.Technician.Sub;
        var visit = rescheduled.VisitId.ToString();
        (string, string, string, string)[] expected =
        [
            ("reprogramacion_propuesta", client, "cliente", visit),
            ("reprogramacion_propuesta", client, "cliente", visit),
            ("reprogramacion_sin_cambio", sub, "tecnico", visit),
            ("visita_reprogramada", client, "cliente", visit),
            ("visita_reprogramada", sub, "tecnico", visit),
            ("cliente_ausente", client, "cliente", visit),
            ("visita_cancelada", cancelled.Technician.Sub, "tecnico", cancelled.VisitId.ToString()),
        ];
        notices.Should().BeEquivalentTo(expected);
        var proposals = await host.OutboxEventsOfTypeAsync("ReprogramacionPropuesta");
        proposals.Should().HaveCount(2).And.OnlyContain(x => x["datos"]["propuestaPor"] == "tecnico");
        var absence = (await host.OutboxEventsOfTypeAsync("ClienteAusente")).Should().ContainSingle().Subject;
        absence["datos"].AsBsonDocument.Names.Should().BeEquivalentTo("visitaId", "solicitudId", "clienteId", "tecnicoUsuarioId");
    }

    private static async Task ProposeAndReplyAsync(Spec020Host host, ConfirmedVisit visit, int days, bool accept)
    {
        using var proposal = await host.PostAsync(visit.VisitId, "reprogramar", visit.Technician.Sub, new { fechaHora = DateTimeOffset.UtcNow.AddDays(days) });
        proposal.StatusCode.Should().Be(HttpStatusCode.OK);
        using var reply = await host.PostAsync(visit.VisitId, "reprogramar/respuesta", host.ClientSub, new { acepta = accept });
        reply.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task DispatchAllAsync(Spec020Host host)
    {
        var pending = true;
        for (var i = 0; i < 50 && pending; i++)
        {
            await using var scope = host.Services.CreateAsyncScope();
            pending = await scope.ServiceProvider.GetRequiredService<Bandeja.DespachadorDeBandeja>().DispatchOneAsync(CancellationToken.None);
        }
    }

    private static (string Type, string Person, string Role, string Resource) Notice(string body)
    {
        var json = JsonElement.Parse(body);
        return (json.GetProperty("tipo").GetString()!, json.GetProperty("destinatarioId").GetString()!, json.GetProperty("rol").GetString()!, json.GetProperty("recurso").GetString()!);
    }
}
