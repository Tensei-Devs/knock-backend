using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>Spec 050, T-02: agenda del técnico (VIS-02) sobre MongoDB real; solo datos ficticios. Las corre el CI.</summary>
public sealed class Spec050AgendaTests
{
    [Fact]
    public async Task Tecnico_Ve_Su_Agenda_Con_200_Y_Sin_Datos_Del_Cliente_CA_050_01()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();

        using var response = await host.SendAsync(HttpMethod.Get, "/api/v1/agenda", visit.Technician.Sub);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var raw = await response.Content.ReadAsStringAsync();
        var item = JsonElement.Parse(raw).GetProperty("items").EnumerateArray().Single();
        item.GetProperty("id").GetString().Should().Be(visit.VisitId.ToString());
        item.GetProperty("estado").GetString().Should().Be("confirmada");
        item.GetProperty("modalidad").GetString().Should().NotBeNullOrEmpty();
        item.GetProperty("dia").GetString().Should().Be((await host.VisitDocAsync(visit.VisitId))["dia"].AsString);
        item.GetProperty("hora").GetString().Should().NotBeNullOrEmpty();
        item.TryGetProperty("fechaHora", out _).Should().BeTrue();
        Spec020Host.CarriesValue(raw, host.ClientSub).Should().BeFalse("la agenda no lleva datos del cliente");
        Spec020Host.CarriesValue(raw, visit.Code!).Should().BeFalse("la agenda no lleva el código de la visita");
        raw.Should().NotContain("Calle Ficticia");
    }

    [Fact]
    public async Task Agenda_Sin_Token_Responde_401_CA_050_02()
    {
        await using var host = await Spec050Host.StartAsync();

        using var response = await host.SendAsync(HttpMethod.Get, "/api/v1/agenda");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Agenda_De_Quien_No_Es_Tecnico_Responde_403_CA_050_03()
    {
        await using var host = await Spec050Host.StartAsync();
        await host.ConfirmedVisitAsync();

        using var response = await host.SendAsync(HttpMethod.Get, "/api/v1/agenda", host.ClientSub);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Spec050Host.CodeOfAsync(response)).Should().Be("not_a_technician");
    }

    [Fact]
    public async Task Agenda_Pagina_De_20_Con_Cursor_Y_Limite_Mayor_A_50_Responde_422_CA_050_05()
    {
        await using var host = await Spec050Host.StartAsync();
        var visit = await host.ConfirmedVisitAsync();
        var template = await host.VisitDocAsync(visit.VisitId);
        var visits = host.Database.GetCollection<BsonDocument>("visitas");
        for (var i = 0; i < 20; i++)
        {
            var copy = template.DeepClone().AsBsonDocument;
            copy["_id"] = ObjectId.GenerateNewId();
            copy["solicitudId"] = ObjectId.GenerateNewId();
            await visits.InsertOneAsync(copy);
        }

        using var first = await host.SendAsync(HttpMethod.Get, "/api/v1/agenda", visit.Technician.Sub);
        var firstPage = await Spec020Host.ReadAsync(first);
        var cursor = firstPage.GetProperty("nextCursor").GetString();
        using var second = await host.SendAsync(HttpMethod.Get, "/api/v1/agenda?cursor=" + Uri.EscapeDataString(cursor!), visit.Technician.Sub);
        var secondPage = await Spec020Host.ReadAsync(second);
        using var tooMany = await host.SendAsync(HttpMethod.Get, "/api/v1/agenda?limite=51", visit.Technician.Sub);

        firstPage.GetProperty("items").GetArrayLength().Should().Be(20, "por omisión la página es de 20");
        cursor.Should().NotBeNullOrEmpty();
        secondPage.GetProperty("items").GetArrayLength().Should().Be(1);
        (!secondPage.TryGetProperty("nextCursor", out var next) || next.ValueKind == JsonValueKind.Null).Should().BeTrue("es la última página");
        var ids = firstPage.GetProperty("items").EnumerateArray().Concat(secondPage.GetProperty("items").EnumerateArray()).Select(i => i.GetProperty("id").GetString());
        ids.Should().OnlyHaveUniqueItems().And.HaveCount(21);
        tooMany.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Spec020Host.ReadAsync(tooMany)).GetProperty("errors").GetProperty("limite").GetArrayLength().Should().BeGreaterThan(0);
    }
}
