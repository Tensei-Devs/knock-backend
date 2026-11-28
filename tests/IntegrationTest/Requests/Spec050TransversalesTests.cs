using System.Net;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>Spec 050, T-11 (R-31 a R-34): la persona es la del <c>sub</c>, registros sin datos personales, 429 y 413, y sin <c>Idempotency-Key</c>.</summary>
public sealed class Spec050TransversalesTests
{
    private const string _noteMarker = "Texto-privado-del-cliente-050";

    [Fact]
    public async Task IdentificadorAjeno_En_El_Cuerpo_Se_Ignora_CA_050_52()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        var stranger = Guid.NewGuid().ToString("D");
        var id = visit.VisitId;

        // Un extraño que nombra al cliente verdadero en el cuerpo no actúa en su nombre: la persona es la del token.
        using var impersonated = await host.SendAsync(HttpMethod.Put, $"/api/v1/visitas/{id}/permiso-fotos", stranger, Foreign(host.ClientSub, "permiso", true));
        using var impersonatedCancel = await host.SendAsync(HttpMethod.Post, $"/api/v1/visitas/{id}/cancelar", stranger, Foreign(host.ClientSub, "motivo", "Motivo con mas de ocho letras"));
        // El dueño con identificadores ajenos en el cuerpo opera igual que sin ellos.
        using var permission = await host.SendAsync(HttpMethod.Put, $"/api/v1/visitas/{id}/permiso-fotos", host.ClientSub, Foreign(stranger, "permiso", true));
        using var cancel = await host.SendAsync(HttpMethod.Post, $"/api/v1/visitas/{id}/cancelar", host.ClientSub, Foreign(stranger, "motivo", "Motivo con mas de ocho letras"));

        impersonated.StatusCode.Should().Be(HttpStatusCode.NotFound);
        impersonatedCancel.StatusCode.Should().Be(HttpStatusCode.NotFound);
        permission.StatusCode.Should().Be(HttpStatusCode.OK);
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        (await host.VisitDocAsync(id))["estado"].AsString.Should().Be("cancelada");
    }

    [Fact]
    public async Task Agenda_Usa_El_Sub_Y_No_Acepta_Otra_Persona_CA_050_52()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        var other = await host.SeedPublicTechnicianAsync();

        using var own = await host.SendAsync(HttpMethod.Get, "/api/v1/agenda?tecnicoId=" + other.Sub, visit.Technician.Sub);
        using var foreignAgenda = await host.SendAsync(HttpMethod.Get, "/api/v1/agenda?tecnicoId=" + visit.Technician.Sub, other.Sub);

        own.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Spec020Host.ReadAsync(own)).GetProperty("items").GetArrayLength().Should().Be(1, "el parámetro tecnicoId se ignora");
        foreignAgenda.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Spec020Host.ReadAsync(foreignAgenda)).GetProperty("items").GetArrayLength().Should().Be(0, "no ve la agenda de otro técnico");
    }

    [Fact]
    public async Task Registros_Y_Errores_Sin_Datos_Personales_CA_050_53()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        var id = visit.VisitId;
        var errorBody = string.Empty;

        var output = await ConsoleCapture.RunAsync(async () =>
        {
            using var agenda = await host.SendAsync(HttpMethod.Get, "/api/v1/agenda", visit.Technician.Sub);
            using var invalid = await host.SendAsync(HttpMethod.Post, $"/api/v1/visitas/{id}/cancelar", host.ClientSub, rawJson: "{\"motivo\":\"corto\"}");
            errorBody = await invalid.Content.ReadAsStringAsync();
            using var report = await host.SendAsync(HttpMethod.Post, $"/api/v1/visitas/{id}/no-terminado", host.ClientSub, new { texto = _noteMarker + " no quedo bien" });
            using var cancel = await host.SendAsync(HttpMethod.Post, $"/api/v1/visitas/{id}/cancelar", host.ClientSub, new { motivo = _noteMarker + " cambio de planes" });
            using var absent = await host.SendAsync(HttpMethod.Post, $"/api/v1/visitas/{id}/cliente-ausente", visit.Technician.Sub);
        });

        output.Should().NotContain(_noteMarker, "los textos escritos por la persona no se registran")
            .And.NotContain("Calle Ficticia", "la dirección no se registra")
            .And.NotContain(host.ClientSub, "el cliente no se registra")
            .And.NotContain(visit.Technician.Sub, "el técnico no se registra")
            .And.NotContain(Spec021Host.TechnicianPhone);
        errorBody.Should().NotContain("corto", "el error nunca repite el valor enviado");
        errorBody.Should().Contain("correlationId").And.Contain("traceId");
    }

    [Fact]
    public async Task Supera_El_Limite_Responde_429_Con_RetryAfter_CA_050_54()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? last = null;

        for (var i = 0; i < 21; i++)
        {
            last?.Dispose();
            last = await host.SendAsync(HttpMethod.Put, $"/api/v1/visitas/{visit.VisitId}/permiso-fotos", host.ClientSub, new { permiso = i % 2 == 0 });
            statuses.Add(last.StatusCode);
        }

        using (last)
        {
            statuses.Take(20).Should().OnlyContain(s => s == HttpStatusCode.OK);
            statuses[20].Should().Be(HttpStatusCode.TooManyRequests);
            last!.Headers.RetryAfter.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Agenda_Supera_60_Por_Minuto_Y_Responde_429_CA_050_54()
    {
        await using var host = await Spec020Host.StartAsync();
        var technician = await host.SeedPublicTechnicianAsync();
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 61; i++)
        {
            using var response = await host.SendAsync(HttpMethod.Get, "/api/v1/agenda", technician.Sub);
            statuses.Add(response.StatusCode);
        }

        statuses.Take(60).Should().OnlyContain(s => s == HttpStatusCode.OK);
        statuses[60].Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Theory]
    [InlineData("POST", "reprogramar")]
    [InlineData("POST", "reprogramar/respuesta")]
    [InlineData("POST", "cancelar")]
    [InlineData("POST", "terminar")]
    [InlineData("POST", "no-terminado")]
    [InlineData("PUT", "permiso-fotos")]
    public async Task Cuerpo_Mayor_A_8_KB_Responde_413_CA_050_54(string method, string route)
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();

        using var response = await host.SendAsync(new HttpMethod(method), $"/api/v1/visitas/{visit.VisitId}/{route}", host.ClientSub, rawJson: "{\"texto\":\"" + new string('a', 9000) + "\"}");

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("payload_too_large");
    }

    [Fact]
    public async Task Repetir_Cancelar_No_Duplica_El_Evento_Ni_Pide_IdempotencyKey_CA_050_55()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        var path = $"/api/v1/visitas/{visit.VisitId}/cancelar";

        using var first = await host.SendAsync(HttpMethod.Post, path, host.ClientSub, new { motivo = "Cambio de planes del cliente" });
        using var second = await host.SendAsync(HttpMethod.Post, path, host.ClientSub, new { motivo = "Cambio de planes del cliente" });

        first.StatusCode.Should().Be(HttpStatusCode.OK, "no exige Idempotency-Key");
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Spec020Host.ReadAsync(second)).GetProperty("code").GetString().Should().Be("visit_already_closed");
        (await EventsOfAsync(host, "VisitaCerrada", visit.VisitId)).Should().Be(1);
    }

    [Fact]
    public async Task Repetir_Reprogramar_Deja_Una_Sola_Propuesta_CA_050_55()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        var path = $"/api/v1/visitas/{visit.VisitId}/reprogramar";
        var proposed = DateTimeOffset.UtcNow.AddDays(3);

        using var first = await host.SendAsync(HttpMethod.Post, path, visit.Technician.Sub, new { fechaHora = proposed });
        using var second = await host.SendAsync(HttpMethod.Post, path, visit.Technician.Sub, new { fechaHora = proposed });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await EventsOfAsync(host, "ReprogramacionPropuesta", visit.VisitId)).Should().Be(1);
    }

    [Fact]
    public async Task Repetir_No_Terminado_Deja_Un_Solo_Reporte_CA_050_55()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        await host.SetVisitAsync(visit.VisitId, Builders<BsonDocument>.Update.Set("estado", "terminada").Set("terminadaEn", DateTime.UtcNow));
        var path = $"/api/v1/visitas/{visit.VisitId}/no-terminado";

        using var first = await host.SendAsync(HttpMethod.Post, path, host.ClientSub, new { texto = "El trabajo quedo a medias" });
        using var second = await host.SendAsync(HttpMethod.Post, path, host.ClientSub, new { texto = "El trabajo quedo a medias" });

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await EventsOfAsync(host, "ReporteNoTerminadoCreado", visit.VisitId)).Should().Be(1);
    }

    private static Dictionary<string, object?> Foreign(string otherPerson, string key, object? value) => new()
    {
        [key] = value,
        ["sub"] = otherPerson,
        ["clienteId"] = otherPerson,
        ["tecnicoId"] = otherPerson,
        ["usuarioId"] = otherPerson,
    };

    private static Task<long> EventsOfAsync(Spec020Host host, string type, ObjectId visitId) =>
        host.CountAsync("outbox", Builders<BsonDocument>.Filter.Eq("tipo", type) & Builders<BsonDocument>.Filter.Eq("agregadoId", visitId.ToString()));
}
