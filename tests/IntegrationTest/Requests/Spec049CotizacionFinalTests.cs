using System.Net;
using AwesomeAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>Spec 049, T-02 y T-03: enviar la cotización final (COT-06) y decidirla (COT-07) sobre MongoDB real; solo datos ficticios. Las corre el CI.</summary>
public sealed class Spec049CotizacionFinalTests
{
    private static readonly string[] _forbiddenKeys = ["precio", "descripcion", "montoNuevo", "texto"];

    [Fact]
    public async Task Tecnico_Envia_La_Cotizacion_Final_Y_Responde_201_CA_049_01()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync();

        using var response = await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var json = await Spec020Host.ReadAsync(response);
        json.GetProperty("visitaId").GetString().Should().Be(visit.VisitId.ToString());
        json.GetProperty("precio").GetInt32().Should().Be(Spec049Host.Price);
        json.GetProperty("estado").GetString().Should().Be("enviada");
        json.GetProperty("venceEn").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow.AddHours(48), TimeSpan.FromMinutes(5));
        json.TryGetProperty("descripcion", out _).Should().BeFalse("la respuesta no repite el texto del técnico");
        var stored = (await host.VisitDocAsync(visit.VisitId))["cotizacionFinal"].AsBsonDocument;
        stored["estado"].AsString.Should().Be("enviada");
        stored["precio"].ToInt32().Should().Be(Spec049Host.Price);
        stored["descripcion"].AsString.Should().Be(Spec049Host.Description);
    }

    [Fact]
    public async Task Sin_Token_Responde_401_Y_No_Guarda_CA_049_02()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync();

        using var response = await host.SendFinalQuoteAsync(visit.VisitId, sub: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.VisitDocAsync(visit.VisitId)).Contains("cotizacionFinal").Should().BeFalse();
    }

    [Fact]
    public async Task Quien_No_Es_Tecnico_Responde_403_Y_No_Cambia_Nada_CA_049_03()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync();

        using var response = await host.SendFinalQuoteAsync(visit.VisitId, host.ClientSub);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("not_a_technician");
        (await host.VisitDocAsync(visit.VisitId)).Contains("cotizacionFinal").Should().BeFalse();
    }

    [Fact]
    public async Task Valor_Invalido_Responde_422_Con_Errores_Sin_Repetir_El_Valor_CA_049_04()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync();
        const string shortText = "abc-049";

        using var price = await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub, new { precio = 0, descripcion = Spec049Host.Description });
        using var description = await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub, new { precio = Spec049Host.Price, descripcion = shortText });

        price.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Spec020Host.ReadAsync(price)).GetProperty("errors").GetProperty("precio").GetArrayLength().Should().BeGreaterThan(0);
        description.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await Spec020Host.ReadAsync(description);
        body.GetProperty("errors").GetProperty("descripcion").GetArrayLength().Should().BeGreaterThan(0);
        Spec020Host.CarriesValue(body.GetRawText(), shortText).Should().BeFalse("el error nunca repite el valor enviado");
        (await host.VisitDocAsync(visit.VisitId)).Contains("cotizacionFinal").Should().BeFalse();
    }

    [Fact]
    public async Task Cuerpo_Ilegible_Responde_400_Y_No_Guarda_CA_049_05()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync();

        using var response = await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub, rawJson: "{ esto no es json");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await host.VisitDocAsync(visit.VisitId)).Contains("cotizacionFinal").Should().BeFalse();
    }

    [Fact]
    public async Task Sin_Visita_En_Curso_O_Ya_Enviada_Responde_409_CA_049_06()
    {
        await using var host = await Spec049Host.StartAsync();
        var confirmed = await host.InProgressAsync();
        await host.SetVisitAsync(confirmed.VisitId, Builders<BsonDocument>.Update.Set("estado", "confirmada"));
        var sent = await host.InProgressAsync();
        var fixedPrice = await host.InProgressAsync("fijo");

        using var notStarted = await host.SendFinalQuoteAsync(confirmed.VisitId, confirmed.Technician.Sub);
        using var first = await host.SendFinalQuoteAsync(sent.VisitId, sent.Technician.Sub);
        using var second = await host.SendFinalQuoteAsync(sent.VisitId, sent.Technician.Sub);
        using var notPreview = await host.SendFinalQuoteAsync(fixedPrice.VisitId, fixedPrice.Technician.Sub);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        foreach (var response in new[] { notStarted, second, notPreview })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("final_quote_not_allowed");
        }

        (await host.VisitDocAsync(confirmed.VisitId)).Contains("cotizacionFinal").Should().BeFalse();
        (await host.VisitDocAsync(fixedPrice.VisitId)).Contains("cotizacionFinal").Should().BeFalse();
    }

    [Fact]
    public async Task Cliente_Aprueba_La_Cotizacion_Final_Y_Responde_200_CA_049_07()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync();
        (await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub)).Dispose();

        using var response = await host.DecideFinalQuoteAsync(visit.VisitId, host.ClientSub);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await Spec020Host.ReadAsync(response);
        json.GetProperty("visitaId").GetString().Should().Be(visit.VisitId.ToString());
        json.GetProperty("estado").GetString().Should().Be("aprobada");
        json.TryGetProperty("resultado", out _).Should().BeFalse("solo aparece al rechazar");
        var document = await host.VisitDocAsync(visit.VisitId);
        document["cotizacionFinal"]["estado"].AsString.Should().Be("aprobada");
        document["estado"].AsString.Should().Be("en_curso", "aprobar no cierra la visita");
    }

    [Fact]
    public async Task Decision_Sin_Token_Responde_401_CA_049_08()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync();
        (await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub)).Dispose();

        using var response = await host.DecideFinalQuoteAsync(visit.VisitId, sub: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.VisitDocAsync(visit.VisitId))["cotizacionFinal"]["estado"].AsString.Should().Be("enviada");
    }

    [Fact]
    public async Task Quien_No_Es_El_Cliente_Dueno_Responde_403_O_404_Y_No_Cambia_Nada_CA_049_09()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync();
        (await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub)).Dispose();

        using var asTechnician = await host.DecideFinalQuoteAsync(visit.VisitId, visit.Technician.Sub);
        using var asStranger = await host.DecideFinalQuoteAsync(visit.VisitId, Guid.NewGuid().ToString("D"));

        asTechnician.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Spec020Host.ReadAsync(asTechnician)).GetProperty("code").GetString().Should().Be("not_the_owner");
        asStranger.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await host.VisitDocAsync(visit.VisitId))["cotizacionFinal"]["estado"].AsString.Should().Be("enviada");
    }

    [Fact]
    public async Task Decision_Invalida_Responde_422_Sin_Repetir_El_Valor_CA_049_10()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync();
        (await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub)).Dispose();

        using var response = await host.DecideFinalQuoteAsync(visit.VisitId, host.ClientSub, new { decision = "tal-vez" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await Spec020Host.ReadAsync(response);
        body.GetProperty("errors").GetProperty("decision").GetArrayLength().Should().BeGreaterThan(0);
        Spec020Host.CarriesValue(body.GetRawText(), "tal-vez").Should().BeFalse();
        (await host.VisitDocAsync(visit.VisitId))["cotizacionFinal"]["estado"].AsString.Should().Be("enviada");
    }

    [Fact]
    public async Task Decision_Con_Cuerpo_Ilegible_Responde_400_CA_049_11()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync();
        (await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub)).Dispose();

        using var response = await host.DecideFinalQuoteAsync(visit.VisitId, host.ClientSub, rawJson: "{ esto no es json");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await host.VisitDocAsync(visit.VisitId))["cotizacionFinal"]["estado"].AsString.Should().Be("enviada");
    }

    [Fact]
    public async Task Sin_Cotizacion_Pendiente_O_Ya_Decidida_O_Vencida_Responde_409_CA_049_12()
    {
        await using var host = await Spec049Host.StartAsync();
        var none = await host.InProgressAsync();
        var decided = await host.InProgressAsync();
        var expired = await host.InProgressAsync();
        (await host.SendFinalQuoteAsync(decided.VisitId, decided.Technician.Sub)).Dispose();
        (await host.SendFinalQuoteAsync(expired.VisitId, expired.Technician.Sub)).Dispose();
        await host.SetVisitAsync(expired.VisitId, Builders<BsonDocument>.Update.Set("cotizacionFinal.venceEn", DateTime.UtcNow.AddMinutes(-1)));

        using var withoutQuote = await host.DecideFinalQuoteAsync(none.VisitId, host.ClientSub);
        using var first = await host.DecideFinalQuoteAsync(decided.VisitId, host.ClientSub);
        using var again = await host.DecideFinalQuoteAsync(decided.VisitId, host.ClientSub, new { decision = "rechaza" });
        using var late = await host.DecideFinalQuoteAsync(expired.VisitId, host.ClientSub);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        foreach (var response in new[] { withoutQuote, again, late })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("final_quote_already_decided");
        }

        (await host.VisitDocAsync(decided.VisitId))["cotizacionFinal"]["estado"].AsString.Should().Be("aprobada", "la decisión no se cambia");
    }

    [Fact]
    public async Task Crea_La_Cotizacion_Y_Avisa_Al_Cliente_Y_La_Segunda_Es_409_CA_049_26()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync();

        using var first = await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub);
        using var second = await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Spec020Host.ReadAsync(second)).GetProperty("code").GetString().Should().Be("final_quote_not_allowed");
        (await host.OutboxOfAsync("CotizacionFinalEnviada", visit.VisitId.ToString())).Should().Be(1, "el aviso al cliente sale una sola vez");
        var task = await host.Database.GetCollection<BsonDocument>("tareas_programadas")
            .Find(new BsonDocument { { "tipo", "VencerCotizacionFinal" }, { "claveNegocio", visit.VisitId.ToString() } }).SingleAsync();
        task["programadaPara"].ToUniversalTime().Should().BeCloseTo(DateTime.UtcNow.AddHours(48), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task Decision_Es_Definitiva_Rechazo_Deja_Solo_Visita_Y_Vencer_Rechaza_CA_049_27()
    {
        await using var host = await Spec049Host.StartAsync();
        var rejected = await host.InProgressAsync();
        var lapsed = await host.InProgressAsync();
        (await host.SendFinalQuoteAsync(rejected.VisitId, rejected.Technician.Sub)).Dispose();
        (await host.SendFinalQuoteAsync(lapsed.VisitId, lapsed.Technician.Sub)).Dispose();

        using var reject = await host.DecideFinalQuoteAsync(rejected.VisitId, host.ClientSub, new { decision = "rechaza" });
        using var change = await host.DecideFinalQuoteAsync(rejected.VisitId, host.ClientSub, new { decision = "aprueba" });
        await host.SetVisitAsync(lapsed.VisitId, Builders<BsonDocument>.Update.Set("cotizacionFinal.venceEn", DateTime.UtcNow.AddMinutes(-1)));
        await host.RunExpiryAsync("VencerCotizacionFinal", lapsed.VisitId);

        reject.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Spec020Host.ReadAsync(reject)).GetProperty("resultado").GetString().Should().Be("solo_visita");
        change.StatusCode.Should().Be(HttpStatusCode.Conflict, "una decisión no puede cambiarse");
        var rejectedDoc = await host.VisitDocAsync(rejected.VisitId);
        rejectedDoc["estado"].AsString.Should().Be("terminada");
        rejectedDoc["resultado"].AsString.Should().Be("solo_visita");
        rejectedDoc["cotizacionFinal"]["estado"].AsString.Should().Be("rechazada");
        var lapsedDoc = await host.VisitDocAsync(lapsed.VisitId);
        lapsedDoc["cotizacionFinal"]["estado"].AsString.Should().Be("vencida", "sin respuesta a las 48 horas se trata como rechazada por vencimiento");
        lapsedDoc["estado"].AsString.Should().Be("terminada");
        lapsedDoc["resultado"].AsString.Should().Be("solo_visita");
        (await host.OutboxOfAsync("VisitaCerrada", rejected.VisitId.ToString())).Should().Be(1);
    }

    [Fact]
    public async Task Eventos_De_La_Cotizacion_Final_Solo_Llevan_Identificadores_CA_049_30()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync();
        (await host.SendFinalQuoteAsync(visit.VisitId, visit.Technician.Sub)).Dispose();
        (await host.DecideFinalQuoteAsync(visit.VisitId, host.ClientSub, new { decision = "rechaza" })).Dispose();

        foreach (var type in new[] { "CotizacionFinalEnviada", "CotizacionFinalDecidida", "VisitaCerrada" })
        {
            var events = await host.OutboxEventsAsync(type);
            events.Should().ContainSingle(type);
            AssertOnlyIdentifiers(events[0]["datos"].AsBsonDocument);
        }
    }

    internal static void AssertOnlyIdentifiers(BsonDocument data)
    {
        data.Names.Should().NotIntersectWith(_forbiddenKeys);
        var json = data.ToJson();
        json.Should().NotContain(Spec049Host.Description).And.NotContain(Spec049Host.Reason);
    }
}
