extern alias MarketApi;

using System.Net;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Common.TareasProgramadas;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>
/// Spec 054, R-02, con las reglas de la spec 050 (R-25 y R-26): los eventos de la visita que pueden ocurrir más de una vez llevan <c>{tipo}:{visitaId}:{version}</c>, así que una segunda
/// propuesta, respuesta o vencimiento, y cancelar una visita bloqueada, no chocan en el buzón. MongoDB real; solo datos ficticios.
/// </summary>
public sealed class Spec054IdDeEventosDeVisitaTests
{
    private const string _proposed = "ReprogramacionPropuesta";
    private const string _rescheduled = "VisitaReprogramada";
    private const string _rejected = "ReprogramacionRechazada";
    private const string _expired = "ReprogramacionVencida";
    private const string _closed = "VisitaCerrada";
    private const string _expiryTask = "VencerPropuestaDeReprogramacion";

    [Fact]
    public async Task Tras_Rechazar_Se_Puede_Proponer_Y_Rechazar_Otra_Vez_CA_054_03()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.VisitAtAsync(TimeSpan.FromDays(3));

        await ProposeAsync(host, visit, 5);
        await ReplyAsync(host, visit, accept: false);
        await ProposeAsync(host, visit, 6);
        await ReplyAsync(host, visit, accept: false);

        (await IdsAsync(host, _proposed, visit)).Should().Equal(Id(_proposed, visit, 1), Id(_proposed, visit, 2));
        (await IdsAsync(host, _rejected, visit)).Should().Equal(Id(_rejected, visit, 1), Id(_rejected, visit, 2));
    }

    [Fact]
    public async Task Se_Aceptan_Dos_Reprogramaciones_Con_Un_Evento_Cada_Una_CA_050_46()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.VisitAtAsync(TimeSpan.FromDays(3));

        await ProposeAsync(host, visit, 5);
        await ReplyAsync(host, visit, accept: true);
        await ProposeAsync(host, visit, 6);
        await ReplyAsync(host, visit, accept: true);
        using var third = await host.PostAsync(visit.VisitId, "reprogramar", visit.Technician.Sub, new { fechaHora = DateTimeOffset.UtcNow.AddDays(7) });

        third.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Spec050Host.CodeOfAsync(third)).Should().Be("visit_not_reschedulable");
        (await IdsAsync(host, _rescheduled, visit)).Should().Equal(Id(_rescheduled, visit, 1), Id(_rescheduled, visit, 2));
    }

    [Fact]
    public async Task Cada_Propuesta_Sin_Respuesta_Vence_Con_Su_Propia_Tarea_CA_050_46()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.VisitAtAsync(TimeSpan.FromDays(3));

        await ProposeAsync(host, visit, 5);
        await ExpireByTaskAsync(host, visit);
        await ProposeAsync(host, visit, 6);
        await ExpireByTaskAsync(host, visit);

        (await host.VisitDocAsync(visit.VisitId)).Contains("propuestaReprogramacion").Should().BeFalse();
        (await IdsAsync(host, _expired, visit)).Should().Equal(Id(_expired, visit, 1), Id(_expired, visit, 2));
    }

    [Fact]
    public async Task Una_Visita_Bloqueada_Se_Puede_Cancelar_CA_050_18()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await BlockedVisitAsync(host);

        using var response = await host.PostAsync(visit.VisitId, "cancelar", host.ClientSub, new { motivo = Spec050Host.Reason });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await host.VisitDocAsync(visit.VisitId))["estado"].AsString.Should().Be("cancelada");
        (await IdsAsync(host, _closed, visit)).Should().Equal(Id(_closed, visit, 1), Id(_closed, visit, 2));
    }

    [Fact]
    public async Task En_Una_Visita_Bloqueada_Se_Registra_Al_Cliente_Ausente_CA_050_24()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await BlockedVisitAsync(host);
        await host.SetAppointmentAsync(visit.VisitId, DateTime.UtcNow.AddMinutes(-30));

        using var response = await host.PostAsync(visit.VisitId, "cliente-ausente", visit.Technician.Sub);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await IdsAsync(host, _closed, visit)).Should().Equal(Id(_closed, visit, 1), Id(_closed, visit, 2));
    }

    [Fact]
    public async Task La_Baja_De_Cuenta_Cancela_Una_Visita_Bloqueada_CA_050_47()
    {
        var settings = new Dictionary<string, string?> { ["Security:InternalTokens:IdentityKey"] = InternalServiceTokens.KeyBase64 };
        await using var host = await Spec020Host.StartAsync(settings);
        var visit = await BlockedVisitAsync(host);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/v1/interno/cuentas/{host.ClientSub}/eliminada", UriKind.Relative));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", InternalServiceTokens.Issue(host.ClientSub, "sin-elemento", ["cuenta"]));

        using var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await host.VisitDocAsync(visit.VisitId))["estado"].AsString.Should().Be("cancelada");
        (await IdsAsync(host, _closed, visit)).Should().Equal(Id(_closed, visit, 1), Id(_closed, visit, 2));
    }

    private static string Id(string type, ConfirmedVisit visit, int version) => $"{type}:{visit.VisitId}:{version}";

    private static async Task<List<string>> IdsAsync(Spec020Host host, string type, ConfirmedVisit visit)
    {
        var filter = Builders<BsonDocument>.Filter;
        var found = await host.Database.GetCollection<BsonDocument>("outbox").Find(filter.Eq("tipo", type) & filter.Eq("agregadoId", visit.VisitId.ToString())).ToListAsync();
        return [.. found.Select(message => message["_id"].AsString).Order(StringComparer.Ordinal)];
    }

    private static async Task ProposeAsync(Spec020Host host, ConfirmedVisit visit, int days)
    {
        using var response = await host.PostAsync(visit.VisitId, "reprogramar", visit.Technician.Sub, new { fechaHora = DateTimeOffset.UtcNow.AddDays(days) });
        response.StatusCode.Should().Be(HttpStatusCode.OK, "la propuesta se guarda");
    }

    private static async Task ReplyAsync(Spec020Host host, ConfirmedVisit visit, bool accept)
    {
        using var response = await host.PostAsync(visit.VisitId, "reprogramar/respuesta", host.ClientSub, new { acepta = accept });
        response.StatusCode.Should().Be(HttpStatusCode.OK, "la respuesta se guarda");
    }

    /// <summary>Adelanta el plazo de la propuesta y de su única tarea pendiente y deja que el ejecutor la tome (es la más antigua).</summary>
    private static async Task ExpireByTaskAsync(Spec020Host host, ConfirmedVisit visit)
    {
        var filter = Builders<BsonDocument>.Filter;
        var update = Builders<BsonDocument>.Update;
        await host.SetVisitAsync(visit.VisitId, update.Set("propuestaReprogramacion.venceEn", DateTime.UtcNow.AddMinutes(-1)));
        var pending = await host.Database.GetCollection<BsonDocument>("tareas_programadas").UpdateManyAsync(
            filter.Eq("tipo", _expiryTask) & filter.Eq("estado", "pendiente"),
            update.Set("programadaPara", new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        pending.MatchedCount.Should().Be(1, "cada propuesta programa su vencimiento");
        using var scope = host.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<EjecutorDeTareasProgramadas>().ExecuteOneAsync(CancellationToken.None)).Should().BeTrue();
    }

    /// <summary>Visita bloqueada por tres códigos incorrectos: sigue <c>confirmada</c> y ya escribió su primer <c>VisitaCerrada</c>.</summary>
    private static async Task<ConfirmedVisit> BlockedVisitAsync(Spec020Host host)
    {
        var visit = await host.ConfirmedVisitAsync();
        await host.OpenWindowAsync(visit.VisitId);
        var wrong = visit.Code == "0000" ? "1111" : "0000";
        for (var attempt = 0; attempt < 3; attempt++)
        {
            (await host.ArriveAsync(visit.VisitId, visit.Technician.Sub, wrong)).Dispose();
        }

        (await host.VisitDocAsync(visit.VisitId))["bloqueada"].AsBoolean.Should().BeTrue();
        return visit;
    }
}
