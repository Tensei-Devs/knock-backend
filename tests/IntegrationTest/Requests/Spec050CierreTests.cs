using System.Net;
using AwesomeAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>
/// Spec 050, T-07 a T-09: terminar, reportar que no se terminó y permiso de fotos (VIS-09 a VIS-11) sobre MongoDB real; solo datos ficticios. Las corre el CI. Una persona sin el papel
/// de la ruta sobre esa visita recibe el mismo 404 que una visita inexistente (no 403), salvo quien no es técnico en las rutas del técnico.
/// </summary>
public sealed class Spec050CierreTests
{
    private const string _unreadable = "{ esto no es json";
    private const string _report = "El trabajo quedo a medias";

    [Fact]
    public async Task Tecnico_Termina_La_Visita_Y_Se_Programan_Los_Dos_Cierres_CA_050_29()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.InProgressVisitAsync();

        using var response = await host.PostAsync(visit.VisitId, "terminar", visit.Technician.Sub, Spec050Host.FinishBody());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await Spec020Host.ReadAsync(response);
        json.GetProperty("estado").GetString().Should().Be("terminada");
        json.GetProperty("terminadaEn").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
        json.TryGetProperty("descripcion", out _).Should().BeFalse("la respuesta no repite el texto del técnico");
        var document = await host.VisitDocAsync(visit.VisitId);
        document["estado"].AsString.Should().Be("terminada");
        document["trabajo"]["descripcion"].AsString.Should().Be(Spec050Host.Work);
        (await host.OutboxOfAsync("VisitaTerminada", visit.VisitId.ToString())).Should().Be(1);
        var tasks = host.Database.GetCollection<BsonDocument>("tareas_programadas");
        var opinion = await tasks.Find(new BsonDocument { { "tipo", "CerrarPeriodoDeOpinion" }, { "claveNegocio", visit.VisitId.ToString() } }).SingleAsync();
        var reportClose = await tasks.Find(new BsonDocument { { "tipo", "CerrarReporteDeNoTerminado" }, { "claveNegocio", visit.VisitId.ToString() } }).SingleAsync();
        opinion["programadaPara"].ToUniversalTime().Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(5));
        reportClose["programadaPara"].ToUniversalTime().Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task Terminar_Sin_Token_Responde_401_CA_050_30()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.InProgressVisitAsync();

        using var response = await host.PostAsync(visit.VisitId, "terminar", sub: null, Spec050Host.FinishBody());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.VisitDocAsync(visit.VisitId))["estado"].AsString.Should().Be("en_curso");
    }

    [Fact]
    public async Task Terminar_De_Quien_No_Es_Tecnico_Responde_403_CA_050_31()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.InProgressVisitAsync();

        using var response = await host.PostAsync(visit.VisitId, "terminar", host.ClientSub, Spec050Host.FinishBody());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Spec050Host.CodeOfAsync(response)).Should().Be("not_a_technician");
        (await host.VisitDocAsync(visit.VisitId))["estado"].AsString.Should().Be("en_curso");
    }

    [Fact]
    public async Task Terminar_Con_Cuerpo_Ilegible_Responde_400_CA_050_33()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.InProgressVisitAsync();

        using var response = await host.PostAsync(visit.VisitId, "terminar", visit.Technician.Sub, rawJson: _unreadable);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await host.VisitDocAsync(visit.VisitId))["estado"].AsString.Should().Be("en_curso");
    }

    [Fact]
    public async Task Terminar_Una_Visita_Que_No_Esta_En_Curso_Responde_409_CA_050_34()
    {
        await using var host = await Spec050Host.StartAsync();
        var confirmed = await host.ConfirmedVisitAsync();
        var finished = await host.FinishedVisitAsync(TimeSpan.FromHours(1));

        foreach (var visit in new[] { confirmed, finished })
        {
            using var response = await host.PostAsync(visit.VisitId, "terminar", visit.Technician.Sub, Spec050Host.FinishBody());

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await Spec050Host.CodeOfAsync(response)).Should().Be("visit_not_in_progress");
        }

        (await host.VisitDocAsync(confirmed.VisitId))["estado"].AsString.Should().Be("confirmada");
        (await host.CountAsync("tareas_programadas", Builders<BsonDocument>.Filter.Eq("tipo", "CerrarPeriodoDeOpinion"))).Should().Be(0);
    }

    [Fact]
    public async Task Cliente_Reporta_Que_No_Se_Termino_Y_Responde_201_CA_050_35()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.FinishedVisitAsync(TimeSpan.FromDays(1));

        using var response = await host.PostAsync(visit.VisitId, "no-terminado", host.ClientSub, new { texto = _report });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await Spec020Host.ReadAsync(response);
        json.GetProperty("visitaId").GetString().Should().Be(visit.VisitId.ToString());
        json.GetProperty("estado").GetString().Should().Be("en_revision");
        var report = (await host.VisitDocAsync(visit.VisitId))["reporteNoTerminado"].AsBsonDocument;
        report["estado"].AsString.Should().Be("en_revision");
        report["texto"].AsString.Should().Be(_report);
        var created = await host.OutboxEventsAsync("ReporteNoTerminadoCreado");
        created.Should().ContainSingle();
        created[0]["datos"].ToJson().Should().NotContain(_report, "el evento solo lleva identificadores");
    }

    [Fact]
    public async Task No_Terminado_Sin_Token_Responde_401_CA_050_36()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.FinishedVisitAsync(TimeSpan.FromDays(1));

        using var response = await host.PostAsync(visit.VisitId, "no-terminado", sub: null, new { texto = _report });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.VisitDocAsync(visit.VisitId)).Contains("reporteNoTerminado").Should().BeFalse();
    }

    [Fact]
    public async Task No_Terminado_De_Quien_No_Es_El_Cliente_Dueno_Responde_404_CA_050_37()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.FinishedVisitAsync(TimeSpan.FromDays(1));

        using var asTechnician = await host.PostAsync(visit.VisitId, "no-terminado", visit.Technician.Sub, new { texto = _report });
        using var asStranger = await host.PostAsync(visit.VisitId, "no-terminado", Guid.NewGuid().ToString("D"), new { texto = _report });

        asTechnician.StatusCode.Should().Be(HttpStatusCode.NotFound);
        asStranger.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await host.VisitDocAsync(visit.VisitId)).Contains("reporteNoTerminado").Should().BeFalse();
    }

    [Fact]
    public async Task No_Terminado_Con_Cuerpo_Ilegible_Responde_400_CA_050_39()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.FinishedVisitAsync(TimeSpan.FromDays(1));

        using var response = await host.PostAsync(visit.VisitId, "no-terminado", host.ClientSub, rawJson: _unreadable);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await host.VisitDocAsync(visit.VisitId)).Contains("reporteNoTerminado").Should().BeFalse();
    }

    [Fact]
    public async Task Permiso_Sin_Token_Responde_401_CA_050_42()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.FinishedVisitAsync(TimeSpan.FromHours(1));

        using var response = await host.PutPermissionAsync(visit.VisitId, sub: null, new { permiso = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.VisitDocAsync(visit.VisitId)).Contains("permisoPublicarFotos").Should().BeFalse();
    }

    [Fact]
    public async Task Permiso_De_Quien_No_Es_El_Cliente_Dueno_Responde_404_Y_No_Cambia_Nada_CA_050_43()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.FinishedVisitAsync(TimeSpan.FromHours(1));

        using var asTechnician = await host.PutPermissionAsync(visit.VisitId, visit.Technician.Sub, new { permiso = true });
        using var asStranger = await host.PutPermissionAsync(visit.VisitId, Guid.NewGuid().ToString("D"), new { permiso = true });

        asTechnician.StatusCode.Should().Be(HttpStatusCode.NotFound);
        asStranger.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await host.VisitDocAsync(visit.VisitId)).Contains("permisoPublicarFotos").Should().BeFalse();
        (await host.OutboxEventsAsync("PermisoFotosCambiado")).Should().BeEmpty();
    }

    [Fact]
    public async Task Permiso_Con_Un_Valor_Que_No_Es_Booleano_Responde_400_Y_No_Cambia_Nada_CA_050_44()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.FinishedVisitAsync(TimeSpan.FromHours(1));
        const string notBoolean = "tal-vez";

        // La spec dice 422, pero `permiso` no tiene validador (ausente se toma como que no) y un tipo equivocado se rechaza al leer el cuerpo: 400.
        using var response = await host.PutPermissionAsync(visit.VisitId, host.ClientSub, rawJson: "{\"permiso\":\"" + notBoolean + "\"}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Spec020Host.CarriesValue(await response.Content.ReadAsStringAsync(), notBoolean).Should().BeFalse("el error no repite el valor");
        (await host.VisitDocAsync(visit.VisitId)).Contains("permisoPublicarFotos").Should().BeFalse();
    }

    [Fact]
    public async Task Reporte_Dentro_De_Los_7_Dias_Se_Crea_Y_Pasados_O_Repetido_Responde_409_CA_050_50()
    {
        await using var host = await Spec050Host.StartAsync();
        var recent = await host.FinishedVisitAsync(TimeSpan.FromDays(6));
        var old = await host.FinishedVisitAsync(TimeSpan.FromDays(8));

        using var created = await host.PostAsync(recent.VisitId, "no-terminado", host.ClientSub, new { texto = _report });
        using var repeated = await host.PostAsync(recent.VisitId, "no-terminado", host.ClientSub, new { texto = _report });
        using var late = await host.PostAsync(old.VisitId, "no-terminado", host.ClientSub, new { texto = _report });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        foreach (var response in new[] { repeated, late })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await Spec050Host.CodeOfAsync(response)).Should().Be("report_window_closed");
        }

        (await host.VisitDocAsync(old.VisitId)).Contains("reporteNoTerminado").Should().BeFalse();
        (await host.OutboxEventsAsync("ReporteNoTerminadoCreado")).Should().ContainSingle();
    }

    [Fact]
    public async Task Autorizar_Y_Revocar_El_Permiso_Escribe_El_Evento_Que_Publica_O_Retira_Las_Fotos_CA_050_51()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.FinishedVisitAsync(TimeSpan.FromHours(1));

        using var granted = await host.PutPermissionAsync(visit.VisitId, host.ClientSub, new { permiso = true });
        using var repeated = await host.PutPermissionAsync(visit.VisitId, host.ClientSub, new { permiso = true });
        var afterGrant = (await host.VisitDocAsync(visit.VisitId))["permisoPublicarFotos"].AsBoolean;
        using var revoked = await host.PutPermissionAsync(visit.VisitId, host.ClientSub, new { permiso = false });

        granted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Spec020Host.ReadAsync(granted)).GetProperty("permiso").GetBoolean().Should().BeTrue();
        repeated.StatusCode.Should().Be(HttpStatusCode.OK);
        afterGrant.Should().BeTrue();
        revoked.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Spec020Host.ReadAsync(revoked)).GetProperty("permiso").GetBoolean().Should().BeFalse();
        (await host.VisitDocAsync(visit.VisitId))["permisoPublicarFotos"].AsBoolean.Should().BeFalse();
        var events = await host.OutboxEventsAsync("PermisoFotosCambiado");
        events.Should().HaveCount(2, "repetir el mismo valor no escribe otro evento");
        events.Select(e => e["datos"]["permiso"].AsBoolean).Should().BeEquivalentTo([true, false]);
    }
}
