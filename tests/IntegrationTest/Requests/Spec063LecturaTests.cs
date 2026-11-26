using System.Net;
using AwesomeAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>Spec 063: leer la cotización final y los ajustes pendientes de la visita como cliente dueño o técnico (MongoDB real, solo datos ficticios).</summary>
public sealed class Spec063LecturaTests
{
    private const string _description = "Cambio de la llave completa y sellado de la conexion";
    private const string _reason = "Hubo que reemplazar tambien la manguera";

    [Fact]
    public async Task Cliente_Lee_La_Cotizacion_Enviada_CA_063_01()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        var expires = DateTime.UtcNow.AddHours(40);
        await SeedFinalQuoteAsync(host, visit.VisitId, "enviada", expires);

        using var response = await QuoteAsync(host, visit.VisitId, host.ClientSub);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await Spec020Host.ReadAsync(response);
        json.GetProperty("visitaId").GetString().Should().Be(visit.VisitId.ToString());
        json.GetProperty("precio").GetInt32().Should().Be(850);
        json.GetProperty("descripcion").GetString().Should().Be(_description);
        json.GetProperty("estado").GetString().Should().Be("enviada");
        json.GetProperty("venceEn").GetDateTimeOffset().Should().BeCloseTo(new DateTimeOffset(expires, TimeSpan.Zero), TimeSpan.FromSeconds(1));
        json.TryGetProperty("decididaEn", out _).Should().BeFalse("aún no se decide");
    }

    [Fact]
    public async Task Tecnico_Lee_El_Mismo_Contenido_CA_063_02()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        await SeedFinalQuoteAsync(host, visit.VisitId, "enviada", DateTime.UtcNow.AddHours(40));

        using var asClient = await QuoteAsync(host, visit.VisitId, host.ClientSub);
        using var asTechnician = await QuoteAsync(host, visit.VisitId, visit.Technician.Sub);

        asTechnician.StatusCode.Should().Be(HttpStatusCode.OK);
        (await asTechnician.Content.ReadAsStringAsync()).Should().Be(await asClient.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ajeno_Inexistente_O_Sin_Cotizacion_Reciben_El_Mismo_404_CA_063_03()
    {
        await using var host = await Spec020Host.StartAsync();
        var withoutQuote = await host.ConfirmedVisitAsync();
        var withQuote = await host.ConfirmedVisitAsync();
        await SeedFinalQuoteAsync(host, withQuote.VisitId, "enviada", DateTime.UtcNow.AddHours(40));

        using var foreign = await QuoteAsync(host, withQuote.VisitId, Guid.NewGuid().ToString("D"));
        using var otherTechnician = await QuoteAsync(host, withQuote.VisitId, withoutQuote.Technician.Sub);
        using var missing = await QuoteAsync(host, ObjectId.GenerateNewId(), host.ClientSub);
        using var noQuote = await QuoteAsync(host, withoutQuote.VisitId, host.ClientSub);

        var shapes = new List<string>();
        foreach (var response in new[] { foreign, otherTechnician, missing, noQuote })
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            var json = await Spec020Host.ReadAsync(response);
            shapes.Add(json.GetProperty("title").GetString() + "|" + json.GetProperty("status").GetInt32() + "|" + json.TryGetProperty("code", out var code) + "|" + code);
        }

        shapes.Distinct().Should().ContainSingle("el 404 es el mismo en los cuatro casos");
    }

    [Fact]
    public async Task Cotizacion_Vencida_Se_Informa_Vencida_Sin_Escribir_CA_063_04()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        await SeedFinalQuoteAsync(host, visit.VisitId, "enviada", DateTime.UtcNow.AddSeconds(-1));
        var before = await host.VisitDocAsync(visit.VisitId);

        using var response = await QuoteAsync(host, visit.VisitId, host.ClientSub);

        (await Spec020Host.ReadAsync(response)).GetProperty("estado").GetString().Should().Be("vencida");
        var after = await host.VisitDocAsync(visit.VisitId);
        after["cotizacionFinal"]["estado"].AsString.Should().Be("enviada", "la lectura no escribe");
        after.ToJson().Should().Be(before.ToJson(), "no cambia ningún campo");
    }

    [Theory]
    [InlineData("aprobada")]
    [InlineData("rechazada")]
    public async Task Cotizacion_Decidida_Trae_Estado_Y_DecididaEn_CA_063_05(string state)
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        var decided = DateTime.UtcNow.AddMinutes(-5);
        await SeedFinalQuoteAsync(host, visit.VisitId, state, DateTime.UtcNow.AddHours(30), decided);

        using var response = await QuoteAsync(host, visit.VisitId, visit.Technician.Sub);

        var json = await Spec020Host.ReadAsync(response);
        json.GetProperty("estado").GetString().Should().Be(state);
        json.GetProperty("decididaEn").GetDateTimeOffset().Should().BeCloseTo(new DateTimeOffset(decided, TimeSpan.Zero), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Ajuste_Pendiente_Vigente_Aparece_Para_Cliente_Y_Tecnico_CA_063_06()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        var sooner = DateTime.UtcNow.AddHours(5);
        var quoteId = ObjectId.GenerateNewId();
        var lateId = await SeedAdjustmentAsync(host, visit, "pendiente", DateTime.UtcNow.AddHours(20), 1500);
        var soonId = await SeedAdjustmentAsync(host, visit, "pendiente", sooner, 1200, quoteId);

        using var asClient = await AdjustmentsAsync(host, visit.VisitId, host.ClientSub);
        using var asTechnician = await AdjustmentsAsync(host, visit.VisitId, visit.Technician.Sub);

        asClient.StatusCode.Should().Be(HttpStatusCode.OK);
        asTechnician.StatusCode.Should().Be(HttpStatusCode.OK);
        (await asTechnician.Content.ReadAsStringAsync()).Should().Be(await asClient.Content.ReadAsStringAsync());
        var items = (await Spec020Host.ReadAsync(asClient)).GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("id").GetString()).Should().Equal(soonId.ToString(), lateId.ToString());
        items[0].GetProperty("cotizacionId").GetString().Should().Be(quoteId.ToString());
        items[0].GetProperty("montoNuevo").GetInt32().Should().Be(1200);
        items[0].GetProperty("motivo").GetString().Should().Be(_reason);
        items[0].GetProperty("estado").GetString().Should().Be("pendiente");
        items[0].GetProperty("venceEn").GetDateTimeOffset().Should().BeCloseTo(new DateTimeOffset(sooner, TimeSpan.Zero), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Sin_Ajustes_O_Con_Uno_Vencido_O_Decidido_Responde_Lista_Vacia_CA_063_07()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        var other = await host.ConfirmedVisitAsync();
        using var none = await AdjustmentsAsync(host, visit.VisitId, host.ClientSub);
        await SeedAdjustmentAsync(host, visit, "pendiente", DateTime.UtcNow.AddSeconds(-1), 1200);
        await SeedAdjustmentAsync(host, visit, "aprobado", DateTime.UtcNow.AddHours(3), 1300);
        await SeedAdjustmentAsync(host, visit, "rechazado", DateTime.UtcNow.AddHours(3), 1400);
        await SeedAdjustmentAsync(host, other, "pendiente", DateTime.UtcNow.AddHours(3), 1500);

        using var response = await AdjustmentsAsync(host, visit.VisitId, host.ClientSub);

        none.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Spec020Host.ReadAsync(none)).GetProperty("items").GetArrayLength().Should().Be(0);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Spec020Host.ReadAsync(response)).GetProperty("items").GetArrayLength().Should().Be(0, "vencido, decidido y de otra visita no se listan");
    }

    [Fact]
    public async Task Ajustes_De_Visita_Ajena_O_Inexistente_Responden_El_Mismo_404_CA_063_03()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        await SeedAdjustmentAsync(host, visit, "pendiente", DateTime.UtcNow.AddHours(3), 1500);

        using var foreign = await AdjustmentsAsync(host, visit.VisitId, Guid.NewGuid().ToString("D"));
        using var missing = await AdjustmentsAsync(host, ObjectId.GenerateNewId(), host.ClientSub);

        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Spec020Host.ReadAsync(foreign)).GetProperty("title").GetString().Should().Be((await Spec020Host.ReadAsync(missing)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Respuestas_No_Se_Cachean_Y_No_Llevan_ETag_CA_063_10()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        await SeedFinalQuoteAsync(host, visit.VisitId, "enviada", DateTime.UtcNow.AddHours(40));

        using var quote = await QuoteAsync(host, visit.VisitId, host.ClientSub);
        using var adjustments = await AdjustmentsAsync(host, visit.VisitId, host.ClientSub);

        foreach (var response in new[] { quote, adjustments })
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.CacheControl!.NoStore.Should().BeTrue();
            response.Headers.CacheControl.Private.Should().BeTrue();
            response.Headers.Contains("ETag").Should().BeFalse();
        }
    }

    [Fact]
    public async Task Las_Respuestas_No_Llevan_Identificadores_De_Personas_CA_063_01()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        await SeedFinalQuoteAsync(host, visit.VisitId, "enviada", DateTime.UtcNow.AddHours(40));
        await SeedAdjustmentAsync(host, visit, "pendiente", DateTime.UtcNow.AddHours(3), 1500);

        using var quote = await QuoteAsync(host, visit.VisitId, host.ClientSub);
        using var adjustments = await AdjustmentsAsync(host, visit.VisitId, host.ClientSub);

        foreach (var response in new[] { quote, adjustments })
        {
            var raw = await response.Content.ReadAsStringAsync();
            Spec020Host.CarriesValue(raw, host.ClientSub).Should().BeFalse();
            Spec020Host.CarriesValue(raw, visit.Technician.Sub).Should().BeFalse();
        }
    }

    private static Task<HttpResponseMessage> QuoteAsync(Spec020Host host, ObjectId visitId, string sub) =>
        host.SendAsync(HttpMethod.Get, $"/api/v1/visitas/{visitId}/cotizacion-final", sub);

    private static Task<HttpResponseMessage> AdjustmentsAsync(Spec020Host host, ObjectId visitId, string sub) =>
        host.SendAsync(HttpMethod.Get, $"/api/v1/visitas/{visitId}/ajustes-pendientes", sub);

    private static Task SeedFinalQuoteAsync(Spec020Host host, ObjectId visitId, string state, DateTime expires, DateTime? decided = null)
    {
        var quote = new BsonDocument { { "precio", 850 }, { "descripcion", _description }, { "estado", state }, { "venceEn", expires }, { "creadoEn", DateTime.UtcNow.AddHours(-1) } };
        if (decided is { } at)
        {
            quote.Add("decididaEn", at);
        }

        return host.SetVisitAsync(visitId, Builders<BsonDocument>.Update.Set("cotizacionFinal", quote));
    }

    private static async Task<ObjectId> SeedAdjustmentAsync(Spec020Host host, ConfirmedVisit visit, string state, DateTime expires, int amount, ObjectId? quoteId = null)
    {
        var id = ObjectId.GenerateNewId();
        await host.Database.GetCollection<BsonDocument>("ajustes_precio").InsertOneAsync(new BsonDocument
        {
            { "_id", id },
            { "visitaId", visit.VisitId },
            { "cotizacionId", quoteId ?? ObjectId.GenerateNewId() },
            { "clienteId", host.ClientSub },
            { "tecnicoUsuarioId", visit.Technician.Sub },
            { "topeAnterior", 1000 },
            { "montoNuevo", amount },
            { "motivo", _reason },
            { "estado", state },
            { "venceEn", expires },
            { "creadoEn", DateTime.UtcNow.AddHours(-1) },
        });
        return id;
    }
}
