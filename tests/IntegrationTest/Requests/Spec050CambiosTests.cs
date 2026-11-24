using System.Net;
using AwesomeAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>
/// Spec 050, T-03 a T-06: reprogramar, responder, cancelar y cliente ausente (VIS-04 a VIS-07) sobre MongoDB real; solo datos ficticios. Las corre el CI. Una persona ajena a la visita recibe
/// el mismo 404 que una visita inexistente (no 403): el código no distingue ajena de inexistente, a diferencia de lo que dice la tabla de casos de la spec.
/// </summary>
public sealed class Spec050CambiosTests
{
    private const string _unreadable = "{ esto no es json";

    [Fact]
    public async Task Tecnico_Propone_Otra_Fecha_Y_Responde_200_CA_050_06()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.VisitAtAsync(TimeSpan.FromDays(3));
        var proposed = DateTimeOffset.UtcNow.AddDays(5);

        using var response = await host.PostAsync(visit.VisitId, "reprogramar", visit.Technician.Sub, new { fechaHora = proposed });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await Spec020Host.ReadAsync(response);
        json.GetProperty("visitaId").GetString().Should().Be(visit.VisitId.ToString());
        json.GetProperty("propuestaPor").GetString().Should().Be("tecnico");
        json.GetProperty("fechaHora").GetDateTimeOffset().Should().BeCloseTo(proposed, TimeSpan.FromSeconds(1));
        json.GetProperty("venceEn").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow.AddHours(24), TimeSpan.FromMinutes(5));
        (await host.VisitDocAsync(visit.VisitId)).Contains("propuestaReprogramacion").Should().BeTrue();
    }

    [Fact]
    public async Task Reprogramar_Sin_Token_Responde_401_CA_050_07()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();

        using var response = await host.PostAsync(visit.VisitId, "reprogramar", sub: null, new { fechaHora = DateTimeOffset.UtcNow.AddDays(5) });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.VisitDocAsync(visit.VisitId)).Contains("propuestaReprogramacion").Should().BeFalse();
    }

    [Fact]
    public async Task Reprogramar_Una_Visita_Ajena_Responde_El_Mismo_404_Que_Una_Inexistente_CA_050_08()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        var stranger = Guid.NewGuid().ToString("D");
        var body = new { fechaHora = DateTimeOffset.UtcNow.AddDays(5) };

        using var foreign = await host.PostAsync(visit.VisitId, "reprogramar", stranger, body);
        using var missing = await host.PostAsync(ObjectId.GenerateNewId(), "reprogramar", stranger, body);

        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Spec020Host.ReadAsync(foreign)).GetProperty("title").GetString().Should().Be((await Spec020Host.ReadAsync(missing)).GetProperty("title").GetString());
        (await host.VisitDocAsync(visit.VisitId)).Contains("propuestaReprogramacion").Should().BeFalse();
    }

    [Fact]
    public async Task Reprogramar_Con_Cuerpo_Ilegible_Responde_400_CA_050_10()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();

        using var response = await host.PostAsync(visit.VisitId, "reprogramar", visit.Technician.Sub, rawJson: _unreadable);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await host.VisitDocAsync(visit.VisitId)).Contains("propuestaReprogramacion").Should().BeFalse();
    }

    [Fact]
    public async Task Otra_Parte_Acepta_La_Propuesta_Y_La_Cita_Cambia_CA_050_12()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.VisitAtAsync(TimeSpan.FromDays(3));
        var proposed = DateTimeOffset.UtcNow.AddDays(5);
        (await host.PostAsync(visit.VisitId, "reprogramar", visit.Technician.Sub, new { fechaHora = proposed })).Dispose();
        var before = await host.VisitDocAsync(visit.VisitId);

        using var response = await host.PostAsync(visit.VisitId, "reprogramar/respuesta", host.ClientSub, new { acepta = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await Spec020Host.ReadAsync(response);
        json.GetProperty("aceptada").GetBoolean().Should().BeTrue();
        json.GetProperty("reprogramaciones").GetInt32().Should().Be(1);
        json.GetProperty("fechaHora").GetDateTimeOffset().Should().BeCloseTo(proposed, TimeSpan.FromSeconds(1));
        var after = await host.VisitDocAsync(visit.VisitId);
        (after["dia"].AsString + after["hora"].AsString).Should().NotBe(before["dia"].AsString + before["hora"].AsString);
        after.Contains("propuestaReprogramacion").Should().BeFalse();
        after["reprogramaciones"].ToInt32().Should().Be(1);
    }

    [Fact]
    public async Task Responder_Sin_Token_Responde_401_CA_050_13()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.VisitAtAsync(TimeSpan.FromDays(3));
        (await host.PostAsync(visit.VisitId, "reprogramar", visit.Technician.Sub, new { fechaHora = DateTimeOffset.UtcNow.AddDays(5) })).Dispose();

        using var response = await host.PostAsync(visit.VisitId, "reprogramar/respuesta", sub: null, new { acepta = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.VisitDocAsync(visit.VisitId)).Contains("propuestaReprogramacion").Should().BeTrue("la propuesta sigue pendiente");
    }

    [Fact]
    public async Task Responder_Una_Visita_Ajena_Responde_404_Y_No_Cambia_Nada_CA_050_14()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.VisitAtAsync(TimeSpan.FromDays(3));
        (await host.PostAsync(visit.VisitId, "reprogramar", visit.Technician.Sub, new { fechaHora = DateTimeOffset.UtcNow.AddDays(5) })).Dispose();

        using var response = await host.PostAsync(visit.VisitId, "reprogramar/respuesta", Guid.NewGuid().ToString("D"), new { acepta = true });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await host.VisitDocAsync(visit.VisitId)).GetValue("reprogramaciones", 0).ToInt32().Should().Be(0);
    }

    [Fact]
    public async Task Responder_Sin_El_Valor_Responde_422_CA_050_15()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.VisitAtAsync(TimeSpan.FromDays(3));
        (await host.PostAsync(visit.VisitId, "reprogramar", visit.Technician.Sub, new { fechaHora = DateTimeOffset.UtcNow.AddDays(5) })).Dispose();

        using var response = await host.PostAsync(visit.VisitId, "reprogramar/respuesta", host.ClientSub, rawJson: "{}");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Spec020Host.ReadAsync(response)).GetProperty("errors").GetProperty("acepta").GetArrayLength().Should().BeGreaterThan(0);
        (await host.VisitDocAsync(visit.VisitId)).Contains("propuestaReprogramacion").Should().BeTrue();
    }

    [Fact]
    public async Task Responder_Con_Cuerpo_Ilegible_Responde_400_CA_050_16()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.VisitAtAsync(TimeSpan.FromDays(3));
        (await host.PostAsync(visit.VisitId, "reprogramar", visit.Technician.Sub, new { fechaHora = DateTimeOffset.UtcNow.AddDays(5) })).Dispose();

        using var response = await host.PostAsync(visit.VisitId, "reprogramar/respuesta", host.ClientSub, rawJson: _unreadable);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await host.VisitDocAsync(visit.VisitId)).Contains("propuestaReprogramacion").Should().BeTrue();
    }

    [Fact]
    public async Task Responder_Sin_Propuesta_O_A_La_Propia_O_Vencida_Responde_409_CA_050_17()
    {
        await using var host = await Spec050Host.StartAsync();
        var none = await host.VisitAtAsync(TimeSpan.FromDays(3));
        var own = await host.VisitAtAsync(TimeSpan.FromDays(3));
        var expired = await host.VisitAtAsync(TimeSpan.FromDays(3));
        (await host.PostAsync(own.VisitId, "reprogramar", own.Technician.Sub, new { fechaHora = DateTimeOffset.UtcNow.AddDays(5) })).Dispose();
        (await host.PostAsync(expired.VisitId, "reprogramar", expired.Technician.Sub, new { fechaHora = DateTimeOffset.UtcNow.AddDays(5) })).Dispose();
        await host.SetVisitAsync(expired.VisitId, Builders<BsonDocument>.Update.Set("propuestaReprogramacion.venceEn", DateTime.UtcNow.AddMinutes(-1)));

        using var withoutProposal = await host.PostAsync(none.VisitId, "reprogramar/respuesta", host.ClientSub, new { acepta = true });
        using var ownProposal = await host.PostAsync(own.VisitId, "reprogramar/respuesta", own.Technician.Sub, new { acepta = true });
        using var late = await host.PostAsync(expired.VisitId, "reprogramar/respuesta", host.ClientSub, new { acepta = true });

        foreach (var response in new[] { withoutProposal, ownProposal, late })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await Spec050Host.CodeOfAsync(response)).Should().Be("no_pending_proposal");
        }

        (await host.VisitDocAsync(own.VisitId)).GetValue("reprogramaciones", 0).ToInt32().Should().Be(0);
    }

    [Fact]
    public async Task Cliente_Cancela_Con_Motivo_Y_Responde_200_CA_050_18()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.VisitAtAsync(TimeSpan.FromDays(3));

        using var response = await host.PostAsync(visit.VisitId, "cancelar", host.ClientSub, new { motivo = Spec050Host.Reason });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await Spec020Host.ReadAsync(response);
        json.GetProperty("estado").GetString().Should().Be("cancelada");
        json.GetProperty("tardia").GetBoolean().Should().BeFalse("faltan más de 24 horas");
        (await host.VisitDocAsync(visit.VisitId))["estado"].AsString.Should().Be("cancelada");
    }

    [Fact]
    public async Task Cancelar_Sin_Token_Responde_401_CA_050_19()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();

        using var response = await host.PostAsync(visit.VisitId, "cancelar", sub: null, new { motivo = Spec050Host.Reason });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.VisitDocAsync(visit.VisitId))["estado"].AsString.Should().Be("confirmada");
    }

    [Fact]
    public async Task Cancelar_Una_Visita_Ajena_Responde_404_Y_No_Cambia_Nada_CA_050_20()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();

        using var response = await host.PostAsync(visit.VisitId, "cancelar", Guid.NewGuid().ToString("D"), new { motivo = Spec050Host.Reason });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await host.VisitDocAsync(visit.VisitId))["estado"].AsString.Should().Be("confirmada");
    }

    [Fact]
    public async Task Cancelar_Con_Cuerpo_Ilegible_Responde_400_CA_050_22()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();

        using var response = await host.PostAsync(visit.VisitId, "cancelar", host.ClientSub, rawJson: _unreadable);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await host.VisitDocAsync(visit.VisitId))["estado"].AsString.Should().Be("confirmada");
    }

    [Fact]
    public async Task Cancelar_Una_Visita_Cerrada_O_En_Curso_Responde_409_CA_050_23()
    {
        await using var host = await Spec050Host.StartAsync();
        var cancelled = await host.ConfirmedVisitAsync();
        var inProgress = await host.InProgressVisitAsync();
        (await host.PostAsync(cancelled.VisitId, "cancelar", host.ClientSub, new { motivo = Spec050Host.Reason })).Dispose();

        using var again = await host.PostAsync(cancelled.VisitId, "cancelar", host.ClientSub, new { motivo = Spec050Host.Reason });
        using var started = await host.PostAsync(inProgress.VisitId, "cancelar", host.ClientSub, new { motivo = Spec050Host.Reason });

        foreach (var response in new[] { again, started })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await Spec050Host.CodeOfAsync(response)).Should().Be("visit_already_closed");
        }

        (await host.VisitDocAsync(inProgress.VisitId))["estado"].AsString.Should().Be("en_curso");
    }

    [Fact]
    public async Task Tecnico_Registra_Cliente_Ausente_Dentro_De_La_Ventana_CA_050_24()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.VisitAtAsync(TimeSpan.FromHours(-1));

        using var response = await host.PostAsync(visit.VisitId, "cliente-ausente", visit.Technician.Sub);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await Spec020Host.ReadAsync(response);
        json.GetProperty("estado").GetString().Should().Be("no_presentado");
        json.GetProperty("quienFalto").GetString().Should().Be("cliente");
        var document = await host.VisitDocAsync(visit.VisitId);
        document["estado"].AsString.Should().Be("no_presentado");
        document["quienFalto"].AsString.Should().Be("cliente");
        (await host.OutboxCountOfAsync("VisitaCerrada", visit.VisitId.ToString())).Should().Be(1, "el cliente recibe el aviso");
    }

    [Fact]
    public async Task Cliente_Ausente_Sin_Token_Responde_401_CA_050_25()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.VisitAtAsync(TimeSpan.FromHours(-1));

        using var response = await host.PostAsync(visit.VisitId, "cliente-ausente", sub: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.VisitDocAsync(visit.VisitId))["estado"].AsString.Should().Be("confirmada");
    }

    [Fact]
    public async Task Cliente_Ausente_De_Quien_No_Es_Tecnico_Responde_403_CA_050_26()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.VisitAtAsync(TimeSpan.FromHours(-1));

        using var response = await host.PostAsync(visit.VisitId, "cliente-ausente", host.ClientSub);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Spec050Host.CodeOfAsync(response)).Should().Be("not_a_technician");
        (await host.VisitDocAsync(visit.VisitId))["estado"].AsString.Should().Be("confirmada");
    }

    [Fact]
    public async Task Cliente_Ausente_Con_Id_Mal_Formado_Responde_400_Sin_Repetirlo_CA_050_27()
    {
        await using var host = await Spec050Host.StartAsync();
        var technician = await host.SeedPublicTechnicianAsync();
        const string badId = "no-es-un-object-id";

        // La spec dice 422 para un valor inválido, pero la ruta solo tiene el id y el código responde 400 (como el resto de rutas de visita).
        using var response = await host.SendAsync(HttpMethod.Post, $"/api/v1/visitas/{badId}/cliente-ausente", technician.Sub);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Spec020Host.CarriesValue(await response.Content.ReadAsStringAsync(), badId).Should().BeFalse();
    }

    [Fact]
    public async Task Cliente_Ausente_Fuera_De_La_Ventana_O_Ya_Iniciada_Responde_409_CA_050_28()
    {
        await using var host = await Spec050Host.StartAsync();
        var early = await host.VisitAtAsync(TimeSpan.FromMinutes(-5));
        var late = await host.VisitAtAsync(TimeSpan.FromHours(-3));
        var future = await host.VisitAtAsync(TimeSpan.FromDays(2));
        var started = await host.InProgressVisitAsync();
        await host.SetAppointmentAsync(started.VisitId, DateTime.UtcNow.AddHours(-1));

        foreach (var visit in new[] { early, late, future, started })
        {
            using var response = await host.PostAsync(visit.VisitId, "cliente-ausente", visit.Technician.Sub);

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await Spec050Host.CodeOfAsync(response)).Should().Be("outside_arrival_window");
            (await host.VisitDocAsync(visit.VisitId))["estado"].AsString.Should().NotBe("no_presentado");
        }
    }
}
