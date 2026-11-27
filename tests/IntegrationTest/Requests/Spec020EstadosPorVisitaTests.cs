using System.Net;
using AwesomeAssertions;
using MongoDB.Bson;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>
/// Enmienda 2026-11-26 de las specs 020 y 029: la solicitud sigue a su visita en la misma transacción. Queda <c>terminada</c> cuando la visita termina y <c>cancelada</c> cuando la visita se
/// cancela (<c>visit_cancelled</c>) o alguna parte no se presenta (<c>visit_no_show</c>). La cotización aceptada no cambia. MongoDB real; solo datos ficticios.
/// </summary>
public sealed class Spec020EstadosPorVisitaTests
{
    [Fact]
    public async Task Terminar_La_Visita_Deja_La_Solicitud_Terminada()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.InProgressVisitAsync();

        using var response = await host.PostAsync(visit.VisitId, "terminar", visit.Technician.Sub, Spec050Host.FinishBody());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var request = await RequestAsync(host, visit.RequestId);
        request["estado"].AsString.Should().Be("terminada");
        request["terminadaEn"].ToUniversalTime().Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        request.Contains("motivoCancelacion").Should().BeFalse();
        await AssertQuoteStaysAcceptedAsync(host, visit);
    }

    [Fact]
    public async Task Rechazar_La_Cotizacion_Final_Deja_La_Solicitud_Terminada()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync();
        (await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub)).Dispose();

        using var response = await host.DecideFinalQuoteAsync(visit.VisitId, host.ClientSub, new { decision = "rechaza" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var visitDoc = await host.VisitDocAsync(visit.VisitId);
        (await RequestAsync(host, visitDoc["solicitudId"].AsObjectId))["estado"].AsString.Should().Be("terminada");
    }

    [Fact]
    public async Task Vencer_La_Cotizacion_Final_Deja_La_Solicitud_Terminada()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync();
        (await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub)).Dispose();
        await host.SetVisitAsync(visit.VisitId, MongoDB.Driver.Builders<BsonDocument>.Update.Set("cotizacionFinal.venceEn", DateTime.UtcNow.AddMinutes(-1)));

        await host.RunExpiryAsync("VencerCotizacionFinal", visit.VisitId);

        var visitDoc = await host.VisitDocAsync(visit.VisitId);
        (await RequestAsync(host, visitDoc["solicitudId"].AsObjectId))["estado"].AsString.Should().Be("terminada");
    }

    [Fact]
    public async Task Aprobar_La_Cotizacion_Final_No_Cambia_La_Solicitud()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync();
        (await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub)).Dispose();

        using var response = await host.DecideFinalQuoteAsync(visit.VisitId, host.ClientSub, new { decision = "aprueba" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var visitDoc = await host.VisitDocAsync(visit.VisitId);
        (await RequestAsync(host, visitDoc["solicitudId"].AsObjectId))["estado"].AsString.Should().Be("cita_confirmada");
    }

    [Fact]
    public async Task Cancelar_La_Visita_Deja_La_Solicitud_Cancelada_Y_Cancelarla_Otra_Vez_Repite_El_Motivo()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.VisitAtAsync(TimeSpan.FromDays(3));

        using var response = await host.PostAsync(visit.VisitId, "cancelar", host.ClientSub, new { motivo = Spec050Host.Reason });
        using var again = await host.SendAsync(HttpMethod.Post, $"/api/v1/solicitudes/{visit.RequestId}/cancelar", host.ClientSub, new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertCancelledAsync(host, visit, "visit_cancelled");
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Spec020Host.ReadAsync(again)).GetProperty("motivo").GetString().Should().Be("visit_cancelled");
    }

    [Fact]
    public async Task Cliente_Ausente_Deja_La_Solicitud_Cancelada_Por_No_Presentarse()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.VisitAtAsync(TimeSpan.FromMinutes(-30));

        using var response = await host.PostAsync(visit.VisitId, "cliente-ausente", visit.Technician.Sub);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertCancelledAsync(host, visit, "visit_no_show");
    }

    [Fact]
    public async Task Tecnico_Ausente_Deja_La_Solicitud_Cancelada_Por_No_Presentarse()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        await Spec064TecnicoAusenteTests.SetAppointmentAsync(host, visit.VisitId, minutesAgo: 20);

        using var response = await Spec064TecnicoAusenteTests.ReportAsync(host, visit.VisitId, host.ClientSub);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertCancelledAsync(host, visit, "visit_no_show");
    }

    [Theory]
    [InlineData("visit_cancelled")]
    [InlineData("visit_no_show")]
    public async Task Motivos_Del_Sistema_No_Se_Aceptan_Como_Entrada(string reason)
    {
        await using var host = await Spec020Host.StartAsync();
        var requestId = await host.CreateRequestAsync(workshop: false);

        using var response = await host.SendAsync(HttpMethod.Post, $"/api/v1/solicitudes/{requestId}/cancelar", host.ClientSub, new { motivo = reason });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await RequestAsync(host, requestId))["estado"].AsString.Should().Be("enviada");
    }

    [Fact]
    public async Task Tecnico_Elegido_Sigue_Viendo_La_Solicitud_Cancelada_Por_La_Visita_Y_Otro_Recibe_404_CA_029_10()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.VisitAtAsync(TimeSpan.FromDays(3));
        var other = await host.SeedPublicTechnicianAsync();
        (await host.PostAsync(visit.VisitId, "cancelar", host.ClientSub, new { motivo = Spec050Host.Reason })).Dispose();

        using var chosen = await host.SendAsync(HttpMethod.Get, $"/api/v1/solicitudes/{visit.RequestId}", visit.Technician.Sub);
        using var stranger = await host.SendAsync(HttpMethod.Get, $"/api/v1/solicitudes/{visit.RequestId}", other.Sub);

        chosen.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await Spec020Host.ReadAsync(chosen);
        json.GetProperty("estado").GetString().Should().Be("cancelada");
        (json.TryGetProperty("direccion", out var address) && address.ValueKind != System.Text.Json.JsonValueKind.Null).Should().BeFalse("el técnico nunca recibe la dirección");
        stranger.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Terminar_Y_Opinar_Deja_La_Solicitud_Calificada()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.InProgressVisitAsync();
        (await host.PostAsync(visit.VisitId, "terminar", visit.Technician.Sub, Spec050Host.FinishBody())).Dispose();

        using var review = await host.SendAsync(HttpMethod.Post, $"/api/v1/visitas/{visit.VisitId}/opinion", host.ClientSub, new { calificacion = 5 });

        review.StatusCode.Should().Be(HttpStatusCode.Created);
        (await RequestAsync(host, visit.RequestId))["estado"].AsString.Should().Be("calificada");
    }

    private static Task<BsonDocument> RequestAsync(Spec020Host host, ObjectId requestId) => host.OneAsync("solicitudes", requestId);

    private static async Task AssertCancelledAsync(Spec020Host host, ConfirmedVisit visit, string reason)
    {
        var request = await RequestAsync(host, visit.RequestId);
        request["estado"].AsString.Should().Be("cancelada");
        request["motivoCancelacion"].AsString.Should().Be(reason);
        request["canceladaEn"].ToUniversalTime().Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        await AssertQuoteStaysAcceptedAsync(host, visit);
    }

    private static async Task AssertQuoteStaysAcceptedAsync(Spec020Host host, ConfirmedVisit visit)
    {
        var quoteId = (await host.VisitDocAsync(visit.VisitId))["cotizacionId"].AsObjectId;
        (await host.OneAsync("cotizaciones", quoteId))["estado"].AsString.Should().Be("aceptada");
    }
}
