using System.Net;
using AwesomeAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>Spec 049, T-04, T-05 y T-06: proponer el ajuste de precio (AJU-01) y decidirlo (AJU-02) sobre MongoDB real; solo datos ficticios. Las corre el CI.</summary>
public sealed class Spec049AjusteTests
{
    private const int _newAmount = Spec049Host.Cap + 500;

    [Fact]
    public async Task Tecnico_Propone_El_Ajuste_Y_Responde_201_CA_049_13()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync("fijo");

        using var response = await host.ProposeAdjustmentAsync(visit.QuoteId, visit.Technician.Sub);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var json = await Spec020Host.ReadAsync(response);
        json.GetProperty("cotizacionId").GetString().Should().Be(visit.QuoteId.ToString());
        json.GetProperty("visitaId").GetString().Should().Be(visit.VisitId.ToString());
        json.GetProperty("montoNuevo").GetInt32().Should().Be(_newAmount);
        json.GetProperty("estado").GetString().Should().Be("pendiente");
        json.GetProperty("venceEn").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow.AddHours(24), TimeSpan.FromMinutes(5));
        var stored = await host.AdjustmentDocAsync(ObjectId.Parse(json.GetProperty("id").GetString()!));
        stored["estado"].AsString.Should().Be("pendiente");
        stored["motivo"].AsString.Should().Be(Spec049Host.Reason);
        (await host.QuoteDocAsync(visit.QuoteId))["tope"].ToInt32().Should().Be(visit.Cap, "el tope no cambia hasta que el cliente apruebe");
    }

    [Fact]
    public async Task Ajuste_Sin_Token_Responde_401_Y_No_Guarda_CA_049_14()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync("fijo");

        using var response = await host.ProposeAdjustmentAsync(visit.QuoteId, sub: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.CountAsync("ajustes_precio")).Should().Be(0);
    }

    [Fact]
    public async Task Ajuste_De_Quien_No_Es_Tecnico_Responde_403_CA_049_15()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync("fijo");

        using var response = await host.ProposeAdjustmentAsync(visit.QuoteId, host.ClientSub);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("not_a_technician");
        (await host.CountAsync("ajustes_precio")).Should().Be(0);
    }

    [Fact]
    public async Task Ajuste_Con_Valor_Invalido_Responde_422_Sin_Repetirlo_CA_049_16()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync("fijo");
        const string shortReason = "abc-049";

        using var amount = await host.ProposeAdjustmentAsync(visit.QuoteId, visit.Technician.Sub, new { montoNuevo = 0, motivo = Spec049Host.Reason });
        using var belowMinimum = await host.ProposeAdjustmentAsync(visit.QuoteId, visit.Technician.Sub, new { montoNuevo = visit.Cap + 50, motivo = Spec049Host.Reason });
        using var reason = await host.ProposeAdjustmentAsync(visit.QuoteId, visit.Technician.Sub, new { montoNuevo = _newAmount, motivo = shortReason });

        foreach (var (response, field) in new[] { (amount, "montoNuevo"), (belowMinimum, "montoNuevo"), (reason, "motivo") })
        {
            response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            var body = await Spec020Host.ReadAsync(response);
            body.GetProperty("errors").GetProperty(field).GetArrayLength().Should().BeGreaterThan(0);
            Spec020Host.CarriesValue(body.GetRawText(), shortReason).Should().BeFalse("el error nunca repite el valor enviado");
        }

        (await host.CountAsync("ajustes_precio")).Should().Be(0);
    }

    [Fact]
    public async Task Ajuste_Con_Cuerpo_Ilegible_Responde_400_CA_049_17()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync("fijo");

        using var response = await host.ProposeAdjustmentAsync(visit.QuoteId, visit.Technician.Sub, rawJson: "{ esto no es json");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await host.CountAsync("ajustes_precio")).Should().Be(0);
    }

    [Fact]
    public async Task Ajuste_Con_Otro_Pendiente_O_Cotizacion_No_Aceptada_Responde_409_CA_049_18()
    {
        await using var host = await Spec049Host.StartAsync();
        var pending = await host.InProgressAsync("fijo");
        var notAccepted = await host.InProgressAsync("fijo");
        await host.Database.GetCollection<BsonDocument>("cotizaciones").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", notAccepted.QuoteId), Builders<BsonDocument>.Update.Set("estado", "enviada"));
        await host.ProposeAsync(pending);

        using var second = await host.ProposeAdjustmentAsync(pending.QuoteId, pending.Technician.Sub);
        using var withoutAcceptance = await host.ProposeAdjustmentAsync(notAccepted.QuoteId, notAccepted.Technician.Sub);

        foreach (var response in new[] { second, withoutAcceptance })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("adjustment_not_allowed");
        }

        (await host.CountAsync("ajustes_precio")).Should().Be(1);
    }

    [Fact]
    public async Task Cliente_Aprueba_El_Ajuste_Y_Responde_200_CA_049_19()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync("fijo");
        var adjustment = await host.ProposeAsync(visit);

        using var response = await host.DecideAdjustmentAsync(adjustment, host.ClientSub);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await Spec020Host.ReadAsync(response);
        json.GetProperty("id").GetString().Should().Be(adjustment.ToString());
        json.GetProperty("estado").GetString().Should().Be("aprobado");
        json.GetProperty("topeNuevo").GetInt32().Should().Be(_newAmount);
        (await host.AdjustmentDocAsync(adjustment))["estado"].AsString.Should().Be("aprobado");
    }

    [Fact]
    public async Task Decision_De_Ajuste_Sin_Token_Responde_401_CA_049_20()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync("fijo");
        var adjustment = await host.ProposeAsync(visit);

        using var response = await host.DecideAdjustmentAsync(adjustment, sub: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.AdjustmentDocAsync(adjustment))["estado"].AsString.Should().Be("pendiente");
    }

    [Fact]
    public async Task Decision_De_Ajuste_De_Quien_No_Es_El_Dueno_Responde_403_O_404_CA_049_21()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync("fijo");
        var adjustment = await host.ProposeAsync(visit);

        using var asTechnician = await host.DecideAdjustmentAsync(adjustment, visit.Technician.Sub);
        using var asStranger = await host.DecideAdjustmentAsync(adjustment, Guid.NewGuid().ToString("D"));

        asTechnician.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Spec020Host.ReadAsync(asTechnician)).GetProperty("code").GetString().Should().Be("not_the_owner");
        asStranger.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await host.AdjustmentDocAsync(adjustment))["estado"].AsString.Should().Be("pendiente");
    }

    [Fact]
    public async Task Decision_De_Ajuste_Invalida_Responde_422_CA_049_22()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync("fijo");
        var adjustment = await host.ProposeAsync(visit);

        using var response = await host.DecideAdjustmentAsync(adjustment, host.ClientSub, new { decision = "tal-vez" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await Spec020Host.ReadAsync(response);
        body.GetProperty("errors").GetProperty("decision").GetArrayLength().Should().BeGreaterThan(0);
        Spec020Host.CarriesValue(body.GetRawText(), "tal-vez").Should().BeFalse();
        (await host.AdjustmentDocAsync(adjustment))["estado"].AsString.Should().Be("pendiente");
    }

    [Fact]
    public async Task Decision_De_Ajuste_Con_Cuerpo_Ilegible_Responde_400_CA_049_23()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync("fijo");
        var adjustment = await host.ProposeAsync(visit);

        using var response = await host.DecideAdjustmentAsync(adjustment, host.ClientSub, rawJson: "{ esto no es json");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await host.AdjustmentDocAsync(adjustment))["estado"].AsString.Should().Be("pendiente");
    }

    [Fact]
    public async Task Decision_De_Ajuste_Inexistente_Responde_404_CA_049_24()
    {
        await using var host = await Spec049Host.StartAsync();

        using var response = await host.DecideAdjustmentAsync(ObjectId.GenerateNewId(), host.ClientSub);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await host.CountAsync("ajustes_precio")).Should().Be(0);
    }

    [Fact]
    public async Task Decision_De_Ajuste_Ya_Decidido_O_Vencido_Responde_409_CA_049_25()
    {
        await using var host = await Spec049Host.StartAsync();
        var decided = await host.InProgressAsync("fijo");
        var expired = await host.InProgressAsync("fijo");
        var decidedId = await host.ProposeAsync(decided);
        var expiredId = await host.ProposeAsync(expired);
        await host.Database.GetCollection<BsonDocument>("ajustes_precio").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", expiredId), Builders<BsonDocument>.Update.Set("venceEn", DateTime.UtcNow.AddMinutes(-1)));

        using var first = await host.DecideAdjustmentAsync(decidedId, host.ClientSub, new { decision = "rechaza" });
        using var again = await host.DecideAdjustmentAsync(decidedId, host.ClientSub);
        using var late = await host.DecideAdjustmentAsync(expiredId, host.ClientSub);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        foreach (var response in new[] { again, late })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("adjustment_already_decided");
        }

        (await host.AdjustmentDocAsync(decidedId))["estado"].AsString.Should().Be("rechazado", "la decisión no se cambia");
    }

    [Fact]
    public async Task Propone_Con_Motivo_Valido_Y_Con_Uno_Pendiente_Responde_409_CA_049_28()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync("fijo");

        var adjustment = await host.ProposeAsync(visit);
        using var second = await host.ProposeAdjustmentAsync(visit.QuoteId, visit.Technician.Sub);

        (await host.AdjustmentDocAsync(adjustment))["estado"].AsString.Should().Be("pendiente");
        (await host.QuoteDocAsync(visit.QuoteId)).Contains("ajustePendienteId").Should().BeTrue();
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Spec020Host.ReadAsync(second)).GetProperty("code").GetString().Should().Be("adjustment_not_allowed");
    }

    [Fact]
    public async Task Precio_Cambia_Solo_Si_Se_Aprueba_Y_Vence_Sin_Cambiarlo_CA_049_29()
    {
        await using var host = await Spec049Host.StartAsync();
        var approved = await host.InProgressAsync("fijo");
        var rejected = await host.InProgressAsync("fijo");
        var lapsed = await host.InProgressAsync("fijo");
        var approvedId = await host.ProposeAsync(approved);
        var rejectedId = await host.ProposeAsync(rejected);
        var lapsedId = await host.ProposeAsync(lapsed);

        (await host.DecideAdjustmentAsync(approvedId, host.ClientSub)).Dispose();
        (await host.DecideAdjustmentAsync(rejectedId, host.ClientSub, new { decision = "rechaza" })).Dispose();
        await host.Database.GetCollection<BsonDocument>("ajustes_precio").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", lapsedId), Builders<BsonDocument>.Update.Set("venceEn", DateTime.UtcNow.AddMinutes(-1)));
        await host.RunExpiryAsync("VencerAjuste", lapsedId);

        (await host.QuoteDocAsync(approved.QuoteId))["tope"].ToInt32().Should().Be(_newAmount);
        (await host.VisitDocAsync(approved.VisitId))["tope"].ToInt32().Should().Be(_newAmount);
        (await host.AdjustmentDocAsync(rejectedId))["estado"].AsString.Should().Be("rechazado");
        (await host.QuoteDocAsync(rejected.QuoteId))["tope"].ToInt32().Should().Be(rejected.Cap);
        (await host.AdjustmentDocAsync(lapsedId))["estado"].AsString.Should().Be("vencido");
        (await host.QuoteDocAsync(lapsed.QuoteId))["tope"].ToInt32().Should().Be(lapsed.Cap);
        (await host.QuoteDocAsync(lapsed.QuoteId)).Contains("ajustePendienteId").Should().BeFalse("el vencimiento libera la cotización");
    }

    [Fact]
    public async Task Eventos_Del_Ajuste_Solo_Llevan_Identificadores_CA_049_30()
    {
        await using var host = await Spec049Host.StartAsync();
        var visit = await host.InProgressAsync("fijo");
        var adjustment = await host.ProposeAsync(visit);
        (await host.DecideAdjustmentAsync(adjustment, host.ClientSub)).Dispose();

        foreach (var type in new[] { "AjustePrecioPropuesto", "AjustePrecioDecidido" })
        {
            var events = await host.OutboxEventsAsync(type);
            events.Should().ContainSingle(type);
            Spec049CotizacionFinalTests.AssertOnlyIdentifiers(events[0]["datos"].AsBsonDocument);
        }
    }
}
