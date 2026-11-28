using System.Net;
using AwesomeAssertions;
using MongoDB.Bson;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>Spec 064: autorización por recurso, sesión obligatoria, validación del <c>id</c> y límite de la ruta «el técnico no llegó» (MongoDB real, solo datos ficticios).</summary>
public sealed class Spec064TecnicoAusenteSeguridadTests
{
    private const string _badId = "no-es-un-id-zq9";

    [Fact]
    public async Task Tecnico_Ajeno_O_Inexistente_Responden_El_Mismo_404_Y_No_Cambia_Nada_CA_064_04()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        await Spec064TecnicoAusenteTests.SetAppointmentAsync(host, visit.VisitId, minutesAgo: 20);
        var otherTechnician = await host.SeedPublicTechnicianAsync();

        using var asTechnician = await Spec064TecnicoAusenteTests.ReportAsync(host, visit.VisitId, visit.Technician.Sub);
        using var asOtherTechnician = await Spec064TecnicoAusenteTests.ReportAsync(host, visit.VisitId, otherTechnician.Sub);
        using var asStranger = await Spec064TecnicoAusenteTests.ReportAsync(host, visit.VisitId, Guid.NewGuid().ToString("D"));
        using var missing = await Spec064TecnicoAusenteTests.ReportAsync(host, ObjectId.GenerateNewId(), host.ClientSub);

        var shapes = new List<string>();
        foreach (var response in new[] { asTechnician, asOtherTechnician, asStranger, missing })
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            var json = await Spec020Host.ReadAsync(response);
            shapes.Add(json.GetProperty("title").GetString() + "|" + json.GetProperty("status").GetInt32() + "|" + json.TryGetProperty("code", out _));
        }

        shapes.Distinct().Should().ContainSingle("el 404 es el mismo para el técnico, el ajeno y la visita inexistente");
        (await host.VisitDocAsync(visit.VisitId))["estado"].AsString.Should().Be("confirmada");
        (await host.EventsAsync("TecnicoNoSePresento")).Should().Be(0);
    }

    [Fact]
    public async Task Id_Mal_Formado_Responde_400_Sin_Repetir_El_Valor_Y_Sin_Token_401_CA_064_06()
    {
        await using var host = await Spec020Host.StartAsync();

        using var malformed = await host.SendAsync(HttpMethod.Post, $"/api/v1/visitas/{_badId}/tecnico-ausente", host.ClientSub);
        using var anonymous = await host.SendAsync(HttpMethod.Post, $"/api/v1/visitas/{ObjectId.GenerateNewId()}/tecnico-ausente");

        malformed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var raw = await malformed.Content.ReadAsStringAsync();
        System.Text.Json.JsonElement.Parse(raw).GetProperty("code").GetString().Should().Be("invalid_id");
        raw.Should().NotContain(_badId, "el valor recibido nunca se repite");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Cuerpo_Con_Campos_Se_Ignora_Y_La_Persona_Es_La_Del_Token_CA_064_04()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        await Spec064TecnicoAusenteTests.SetAppointmentAsync(host, visit.VisitId, minutesAgo: 20);
        var stranger = Guid.NewGuid().ToString("D");

        using var impersonated = await host.SendAsync(HttpMethod.Post, $"/api/v1/visitas/{visit.VisitId}/tecnico-ausente", stranger, new { clienteId = host.ClientSub, quienFalto = "cliente" });
        using var owner = await host.SendAsync(HttpMethod.Post, $"/api/v1/visitas/{visit.VisitId}/tecnico-ausente", host.ClientSub, new { clienteId = stranger, quienFalto = "cliente" });

        impersonated.StatusCode.Should().Be(HttpStatusCode.NotFound);
        owner.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Spec020Host.ReadAsync(owner)).GetProperty("quienFalto").GetString().Should().Be("tecnico");
    }

    [Fact]
    public async Task Supera_El_Limite_De_21_Por_Minuto_Y_Responde_429_Con_RetryAfter_CA_064_08()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        await Spec064TecnicoAusenteTests.SetAppointmentAsync(host, visit.VisitId, minutesAgo: 20);

        var responses = await Task.WhenAll(Enumerable.Range(0, 21).Select(_ => Spec064TecnicoAusenteTests.ReportAsync(host, visit.VisitId, host.ClientSub)));

        try
        {
            responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(20);
            var limited = responses.Where(r => r.StatusCode == HttpStatusCode.TooManyRequests).Should().ContainSingle().Subject;
            limited.Headers.RetryAfter.Should().NotBeNull();
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }
}
