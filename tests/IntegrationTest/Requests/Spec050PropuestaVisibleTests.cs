using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>Spec 050, R-35 a R-37: la propuesta de reprogramación vigente se ve igual para las dos partes en <c>GET /visitas/{id}</c>. MongoDB real y datos ficticios.</summary>
public sealed class Spec050PropuestaVisibleTests
{
    private const string _flag = "reprogramacionPendiente";
    private const string _proposal = "propuestaReprogramacion";

    private static async Task<(Spec020Host Host, ConfirmedVisit Visit, DateTimeOffset Proposed)> ProposedAsync()
    {
        var host = await Spec050Host.StartAsync();
        var visit = await host.VisitAtAsync(TimeSpan.FromDays(3));
        var proposed = DateTimeOffset.UtcNow.AddDays(5);
        using var response = await host.PostAsync(visit.VisitId, "reprogramar", visit.Technician.Sub, new { fechaHora = proposed });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (host, visit, proposed);
    }

    private static async Task<JsonElement> ViewAsync(Spec020Host host, ObjectId visitId, string sub)
    {
        using var response = await host.VisitAsync(visitId, sub);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await Spec020Host.ReadAsync(response);
    }

    private static async Task ShouldHideTheProposalAsync(Spec020Host host, ConfirmedVisit visit)
    {
        foreach (var sub in new[] { host.ClientSub, visit.Technician.Sub })
        {
            var view = await ViewAsync(host, visit.VisitId, sub);
            view.GetProperty(_flag).GetBoolean().Should().BeFalse();
            view.TryGetProperty(_proposal, out _).Should().BeFalse("el campo se omite sin una propuesta vigente");
        }
    }

    [Fact]
    public async Task Las_Dos_Partes_Ven_La_Misma_Propuesta_Y_Leer_No_Cambia_Nada_CA_050_56()
    {
        var (host, visit, proposed) = await ProposedAsync();
        await using var disposal = host;
        var before = await host.VisitDocAsync(visit.VisitId);

        var client = await ViewAsync(host, visit.VisitId, host.ClientSub);
        var technician = await ViewAsync(host, visit.VisitId, visit.Technician.Sub);

        client.GetProperty(_flag).GetBoolean().Should().BeTrue();
        technician.GetProperty(_flag).GetBoolean().Should().BeTrue();
        var seen = client.GetProperty(_proposal);
        technician.GetProperty(_proposal).GetRawText().Should().Be(seen.GetRawText(), "las dos partes reciben lo mismo");
        seen.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("fechaHora", "propuestaPor", "venceEn");
        seen.GetProperty("propuestaPor").GetString().Should().Be("tecnico");
        seen.GetProperty("fechaHora").GetDateTimeOffset().Should().BeCloseTo(proposed, TimeSpan.FromSeconds(1));
        seen.GetProperty("fechaHora").GetDateTimeOffset().Offset.Should().Be(TimeSpan.Zero);
        seen.GetProperty("venceEn").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow.AddHours(24), TimeSpan.FromMinutes(5));
        (await host.VisitDocAsync(visit.VisitId)).Equals(before).Should().BeTrue("leer no cambia nada");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Aceptada_O_Rechazada_Ya_No_Se_Muestra_CA_050_57(bool accepted)
    {
        var (host, visit, _) = await ProposedAsync();
        await using var disposal = host;

        using var reply = await host.PostAsync(visit.VisitId, "reprogramar/respuesta", host.ClientSub, new { acepta = accepted });

        reply.StatusCode.Should().Be(HttpStatusCode.OK);
        await ShouldHideTheProposalAsync(host, visit);
    }

    [Fact]
    public async Task Sin_Propuesta_O_Vencida_No_Se_Muestra_CA_050_57()
    {
        var (host, visit, _) = await ProposedAsync();
        await using var disposal = host;
        await host.SetVisitAsync(visit.VisitId, Builders<BsonDocument>.Update.Set("propuestaReprogramacion.venceEn", DateTime.UtcNow.AddMinutes(-1)));

        await ShouldHideTheProposalAsync(host, visit);

        await host.SetVisitAsync(visit.VisitId, Builders<BsonDocument>.Update.Unset("propuestaReprogramacion"));
        await ShouldHideTheProposalAsync(host, visit);
    }

    [Fact]
    public async Task Fuera_De_Confirmada_No_Se_Muestra_Y_La_Guardada_Se_Conserva_CA_050_58()
    {
        var (host, visit, _) = await ProposedAsync();
        await using var disposal = host;
        await host.SetVisitAsync(visit.VisitId, Builders<BsonDocument>.Update.Set("estado", "en_curso").Set("llegadaEn", DateTime.UtcNow));

        await ShouldHideTheProposalAsync(host, visit);

        (await host.VisitDocAsync(visit.VisitId)).Contains("propuestaReprogramacion").Should().BeTrue("leer no borra la propuesta guardada");
    }

    [Fact]
    public async Task Una_Persona_Ajena_Recibe_El_Mismo_404_Y_El_Dueno_La_Sigue_Viendo_CA_050_59()
    {
        var (host, visit, _) = await ProposedAsync();
        await using var disposal = host;
        var stranger = Guid.NewGuid().ToString("D");
        var before = await host.VisitDocAsync(visit.VisitId);

        using var foreign = await host.VisitAsync(visit.VisitId, stranger);
        using var missing = await host.VisitAsync(ObjectId.GenerateNewId(), stranger);
        var control = await ViewAsync(host, visit.VisitId, host.ClientSub);

        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await Spec020Host.ReadAsync(foreign);
        body.GetProperty("title").GetString().Should().Be((await Spec020Host.ReadAsync(missing)).GetProperty("title").GetString());
        body.GetRawText().Should().NotContain(_proposal).And.NotContain("venceEn");
        control.TryGetProperty(_proposal, out _).Should().BeTrue();
        (await host.VisitDocAsync(visit.VisitId)).Equals(before).Should().BeTrue("leer no cambia nada");
    }
}
