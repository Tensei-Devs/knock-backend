using System.Globalization;
using System.Net;
using AwesomeAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>Spec 064: el cliente dueño reporta que el técnico no llegó a la cita (MongoDB real, solo datos ficticios).</summary>
public sealed class Spec064TecnicoAusenteTests
{
    private const string _eventType = "TecnicoNoSePresento";

    [Fact]
    public async Task Cliente_Reporta_Pasados_20_Minutos_Y_La_Visita_Queda_No_Presentado_CA_064_01()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        await SetAppointmentAsync(host, visit.VisitId, minutesAgo: 20);

        using var response = await ReportAsync(host, visit.VisitId, host.ClientSub);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await Spec020Host.ReadAsync(response);
        json.GetProperty("visitaId").GetString().Should().Be(visit.VisitId.ToString());
        json.GetProperty("estado").GetString().Should().Be("no_presentado");
        json.GetProperty("quienFalto").GetString().Should().Be("tecnico");
        var doc = await host.VisitDocAsync(visit.VisitId);
        doc["estado"].AsString.Should().Be("no_presentado");
        doc["quienFalto"].AsString.Should().Be("tecnico");
        doc.Contains("tardia").Should().BeFalse("la bandera tardia no cambia");
    }

    [Fact]
    public async Task Buzon_Tiene_Un_Solo_Evento_Con_Solo_Identificadores_CA_064_02()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        await SetAppointmentAsync(host, visit.VisitId, minutesAgo: 20);

        using var response = await ReportAsync(host, visit.VisitId, host.ClientSub);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var events = await host.Database.GetCollection<BsonDocument>("outbox").Find(Builders<BsonDocument>.Filter.Eq("tipo", _eventType)).ToListAsync();
        var data = events.Should().ContainSingle().Which["datos"].AsBsonDocument;
        data.Names.Should().BeEquivalentTo("visitaId", "solicitudId", "clienteId", "tecnicoUsuarioId");
        data["visitaId"].AsString.Should().Be(visit.VisitId.ToString());
        data["solicitudId"].AsString.Should().Be(visit.RequestId.ToString());
        data["clienteId"].AsString.Should().Be(host.ClientSub);
        data["tecnicoUsuarioId"].AsString.Should().Be(visit.Technician.Sub);
    }

    [Theory]
    [InlineData(10, "confirmada")]
    [InlineData(180, "confirmada")]
    [InlineData(20, "en_curso")]
    [InlineData(20, "terminada")]
    [InlineData(20, "cancelada")]
    public async Task Fuera_De_La_Ventana_O_En_Otro_Estado_Responde_409_Y_No_Cambia_Nada_CA_064_03(int minutesAgo, string state)
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        await SetAppointmentAsync(host, visit.VisitId, minutesAgo);
        await host.SetVisitAsync(visit.VisitId, Builders<BsonDocument>.Update.Set("estado", state));

        using var response = await ReportAsync(host, visit.VisitId, host.ClientSub);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("outside_arrival_window");
        var doc = await host.VisitDocAsync(visit.VisitId);
        doc["estado"].AsString.Should().Be(state);
        doc.Contains("quienFalto").Should().BeFalse();
        (await host.EventsAsync(_eventType)).Should().Be(0);
    }

    [Fact]
    public async Task Repetir_Responde_200_Sin_Segundo_Evento_CA_064_05()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        await SetAppointmentAsync(host, visit.VisitId, minutesAgo: 20);

        using var first = await ReportAsync(host, visit.VisitId, host.ClientSub);
        using var second = await ReportAsync(host, visit.VisitId, host.ClientSub);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Spec020Host.ReadAsync(second)).GetProperty("quienFalto").GetString().Should().Be("tecnico");
        (await host.EventsAsync(_eventType)).Should().Be(1);
    }

    [Fact]
    public async Task Llamadas_Simultaneas_Responden_200_Y_Escriben_Un_Solo_Evento_CA_064_05()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        await SetAppointmentAsync(host, visit.VisitId, minutesAgo: 20);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => ReportAsync(host, visit.VisitId, host.ClientSub)));

        try
        {
            responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        (await host.EventsAsync(_eventType)).Should().Be(1);
    }

    [Fact]
    public async Task Marcada_La_Visita_La_Llegada_Del_Tecnico_Responde_409_Y_No_Cambia_El_Estado_CA_064_07()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        await host.OpenWindowAsync(visit.VisitId);
        await SetAppointmentAsync(host, visit.VisitId, minutesAgo: 20);
        using var report = await ReportAsync(host, visit.VisitId, host.ClientSub);

        using var arrival = await host.ArriveAsync(visit.VisitId, visit.Technician.Sub, visit.Code);

        report.StatusCode.Should().Be(HttpStatusCode.OK);
        arrival.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await host.VisitDocAsync(visit.VisitId))["estado"].AsString.Should().Be("no_presentado");
    }

    internal static Task<HttpResponseMessage> ReportAsync(Spec020Host host, ObjectId visitId, string? sub) =>
        host.SendAsync(HttpMethod.Post, $"/api/v1/visitas/{visitId}/tecnico-ausente", sub);

    /// <summary>Pone la cita hace <paramref name="minutesAgo"/> minutos, con día y hora de Ciudad de México (la hora se guarda sin segundos, así que la cita queda hasta un minuto antes).</summary>
    internal static Task SetAppointmentAsync(Spec020Host host, ObjectId visitId, int minutesAgo)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow.AddMinutes(-minutesAgo), TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City"));
        return host.SetVisitAsync(
            visitId,
            Builders<BsonDocument>.Update.Set("dia", local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Set("hora", local.ToString("HH:mm", CultureInfo.InvariantCulture)));
    }
}
