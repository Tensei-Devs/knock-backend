using System.Net;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>Spec 049, T-07 (R-18 a R-21): la persona es la del <c>sub</c>, registros sin datos personales, 429 y 413, y sin <c>Idempotency-Key</c>. Las corre el CI.</summary>
public sealed class Spec049TransversalesTests
{
    [Fact]
    public async Task Identificadores_Ajenos_En_El_Cuerpo_Se_Ignoran_Y_Manda_El_Sub_CA_049_31()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync("visita");
        var fixedQuote = await host.InProgressAsync("fijo");
        var otherTechnician = await host.SeedPublicTechnicianAsync();
        var stranger = Guid.NewGuid().ToString("D");

        // Un técnico ajeno que nombra al verdadero en el cuerpo no actúa en su nombre.
        using var sendAsOther = await host.SendFinalQuoteAsync(visit.VisitId, otherTechnician.Sub, Foreign(visit.Technician.Sub, ("precio", Spec049Host.Price), ("descripcion", Spec049Host.Description)));
        using var proposeAsOther = await host.ProposeAdjustmentAsync(fixedQuote.QuoteId, otherTechnician.Sub, Foreign(fixedQuote.Technician.Sub, ("montoNuevo", Spec049Host.Cap + 500), ("motivo", Spec049Host.Reason)));
        // El técnico verdadero con identificadores ajenos opera igual que sin ellos.
        using var send = await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub, Foreign(stranger, ("precio", Spec049Host.Price), ("descripcion", Spec049Host.Description)));
        var adjustment = await host.ProposeAsync(fixedQuote);
        // Una persona ajena que nombra al cliente verdadero no decide por él.
        using var decideAsStranger = await host.DecideFinalQuoteAsync(visit.VisitId, stranger, Foreign(host.ClientSub, ("decision", "aprueba")));
        using var decideAdjustmentAsStranger = await host.DecideAdjustmentAsync(adjustment, stranger, Foreign(host.ClientSub, ("decision", "aprueba")));

        sendAsOther.StatusCode.Should().Be(HttpStatusCode.NotFound);
        proposeAsOther.StatusCode.Should().Be(HttpStatusCode.NotFound);
        send.StatusCode.Should().Be(HttpStatusCode.Created);
        decideAsStranger.StatusCode.Should().Be(HttpStatusCode.NotFound);
        decideAdjustmentAsStranger.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await host.VisitDocAsync(visit.VisitId))["cotizacionFinal"]["estado"].AsString.Should().Be("enviada");
        (await host.AdjustmentDocAsync(adjustment))["estado"].AsString.Should().Be("pendiente");
    }

    [Fact]
    public async Task Registros_Y_Errores_Sin_Datos_Personales_CA_049_32()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync("visita");
        var fixedQuote = await host.InProgressAsync("fijo");
        var errorBodies = new List<string>();

        var output = await ConsoleCapture.RunAsync(async () =>
        {
            using var invalid = await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub, new { precio = 0, descripcion = Spec049Host.Description });
            errorBodies.Add(await invalid.Content.ReadAsStringAsync());
            using var sent = await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub);
            using var conflict = await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub);
            errorBodies.Add(await conflict.Content.ReadAsStringAsync());
            using var decided = await host.DecideFinalQuoteAsync(visit.VisitId, host.ClientSub);
            var adjustment = await host.ProposeAsync(fixedQuote);
            using var adjustmentDecision = await host.DecideAdjustmentAsync(adjustment, host.ClientSub);
        });

        output.Should().NotContain(Spec049Host.Description, "el texto del técnico no se registra")
            .And.NotContain(Spec049Host.Reason, "el motivo no se registra")
            .And.NotContain(host.ClientSub, "el cliente no se registra")
            .And.NotContain(visit.Technician.Sub, "el técnico no se registra")
            .And.NotContain(Spec021Host.TechnicianPhone);
        foreach (var body in errorBodies)
        {
            Spec020Host.CarriesValue(body, Spec049Host.Description).Should().BeFalse();
            Spec020Host.CarriesValue(body, host.ClientSub).Should().BeFalse();
            Spec020Host.CarriesValue(body, visit.Technician.Sub).Should().BeFalse();
            body.Should().Contain("correlationId").And.Contain("traceId");
        }
    }

    [Theory]
    [InlineData("final", 10)]
    [InlineData("final-decision", 20)]
    [InlineData("adjustment", 10)]
    [InlineData("adjustment-decision", 20)]
    public async Task Supera_El_Limite_Responde_429_Con_RetryAfter_CA_049_33(string route, int limit)
    {
        await using var host = await Spec049Host.StartAsync();
        var sub = Guid.NewGuid().ToString("D");
        var path = PathOf(route, ObjectId.GenerateNewId());
        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? last = null;

        for (var i = 0; i <= limit; i++)
        {
            last?.Dispose();
            last = await host.SendAsync(HttpMethod.Post, path, sub, rawJson: BodyOf(route));
            statuses.Add(last.StatusCode);
        }

        using (last)
        {
            statuses.Take(limit).Should().NotContain(HttpStatusCode.TooManyRequests);
            statuses[limit].Should().Be(HttpStatusCode.TooManyRequests);
            last!.Headers.RetryAfter.Should().NotBeNull();
        }
    }

    [Theory]
    [InlineData("final")]
    [InlineData("final-decision")]
    [InlineData("adjustment")]
    [InlineData("adjustment-decision")]
    public async Task Cuerpo_Mayor_A_8_KB_Responde_413_CA_049_33(string route)
    {
        await using var host = await Spec049Host.StartAsync();

        using var response = await host.SendAsync(HttpMethod.Post, PathOf(route, ObjectId.GenerateNewId()), Guid.NewGuid().ToString("D"), rawJson: "{\"texto\":\"" + new string('a', 9000) + "\"}");

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("payload_too_large");
    }

    [Fact]
    public async Task Repetir_La_Creacion_No_Deja_Duplicados_Ni_Pide_IdempotencyKey_CA_049_34()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync("visita");
        var fixedQuote = await host.InProgressAsync("fijo");

        using var firstQuote = await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub);
        using var secondQuote = await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub);
        using var firstAdjustment = await host.ProposeAdjustmentAsync(fixedQuote.QuoteId, fixedQuote.Technician.Sub);
        using var secondAdjustment = await host.ProposeAdjustmentAsync(fixedQuote.QuoteId, fixedQuote.Technician.Sub);

        firstQuote.StatusCode.Should().Be(HttpStatusCode.Created, "no exige Idempotency-Key");
        secondQuote.StatusCode.Should().Be(HttpStatusCode.Conflict);
        firstAdjustment.StatusCode.Should().Be(HttpStatusCode.Created);
        secondAdjustment.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await host.OutboxOfAsync("CotizacionFinalEnviada", visit.VisitId.ToString())).Should().Be(1);
        (await host.CountAsync("ajustes_precio")).Should().Be(1);
        (await host.CountAsync("outbox", Builders<BsonDocument>.Filter.Eq("tipo", "AjustePrecioPropuesto"))).Should().Be(1);
    }

    private static string PathOf(string route, ObjectId id) => route switch
    {
        "final" => $"/api/v1/visitas/{id}/cotizacion-final",
        "final-decision" => $"/api/v1/visitas/{id}/cotizacion-final/decision",
        "adjustment" => $"/api/v1/cotizaciones/{id}/ajuste-precio",
        _ => $"/api/v1/ajustes/{id}/decision",
    };

    private static string BodyOf(string route) => route switch
    {
        "final" => "{\"precio\":850,\"descripcion\":\"" + Spec049Host.Description + "\"}",
        "adjustment" => "{\"montoNuevo\":1500,\"motivo\":\"" + Spec049Host.Reason + "\"}",
        _ => "{\"decision\":\"aprueba\"}",
    };

    private static Dictionary<string, object?> Foreign(string otherPerson, params (string Key, object? Value)[] own)
    {
        var body = new Dictionary<string, object?>
        {
            ["sub"] = otherPerson,
            ["clienteId"] = otherPerson,
            ["tecnicoId"] = otherPerson,
            ["tecnicoUsuarioId"] = otherPerson,
            ["usuarioId"] = otherPerson,
        };
        foreach (var (key, value) in own)
        {
            body[key] = value;
        }

        return body;
    }
}
