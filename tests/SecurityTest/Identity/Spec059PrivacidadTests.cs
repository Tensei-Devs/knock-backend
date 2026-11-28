using System.Net;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Knock.SecurityTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.SecurityTest.Identity;

/// <summary>
/// Spec 059, T-06 (CA-17): una suspensión completa por la ruta del panel no deja en eventos, avisos, auditoría, estado de la cuenta ni respuestas el nombre, el texto del reporte ni la identidad de quien reportó.
/// Se buscan valores largos y únicos de la prueba, nunca ids cortos dentro de cuerpos con ids al azar. Solo datos ficticios.
/// </summary>
public sealed class Spec059PrivacidadTests
{
    private const string _reportText = "Texto ficticio del reporte";
    private const string _name = "Nombre Ficticio Unico";

    [Fact]
    public async Task A_Full_Suspension_Leaves_Only_Identifiers_And_Types_And_Never_The_Reporter_CA_059_17()
    {
        await using var factory = await SecurityIdentityFactory.CreateAsync(PanelReportKit.HostSettings);
        using var client = factory.CreateClient();
        var moderator = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["moderador"]);
        var (id, reported) = await PanelReportKit.SeedAccountAsync(factory.Users, technician: true, name: _name);
        var reporter = Guid.NewGuid().ToString("D");
        var reportId = await PanelReportKit.SeedReportAsync(factory.Database, reporter, reported);

        using var response = await PanelReportKit.DecideAsync(client, PanelReportKit.Token(moderator), reportId, new { gravedad = "critica", motivo = "peligro inminente" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertClean(SecureHttp.WithoutCorrelationId(await response.Content.ReadAsStringAsync()), reporter);
        var outbox = await factory.Database.GetCollection<BsonDocument>("outbox").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        outbox.Select(e => e["tipo"].AsString).Should().Contain(["CuentaSuspendida", "ReporteDecidido"]);
        outbox.Single(e => e["tipo"].AsString == "CuentaSuspendida")["carga"].AsBsonDocument.Names.Should().BeEquivalentTo("eventoId", "usuarioId", "reporteId", "desde");
        var seenByTheReported = outbox.Where(e => e["tipo"].AsString == "CuentaSuspendida" || (e["tipo"].AsString == "ReporteDecidido" && e["carga"]["rol"].AsString == "reportado"));
        AssertClean(string.Join(' ', seenByTheReported.Select(e => e.ToJson())), reporter);
        var audit = await factory.Database.GetCollection<BsonDocument>("panel_auditoria").Find(Builders<BsonDocument>.Filter.Eq("recursoId", reportId)).ToListAsync();
        audit.Should().NotBeEmpty();
        AssertClean(string.Join(' ', audit.Select(e => e.ToJson())), reporter);
        var user = await factory.Users.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync();
        user["suspension"].AsBsonDocument.Names.Should().BeEquivalentTo("tipo", "reporteId", "desde");
        AssertClean(user["suspension"].ToJson(), reporter);
    }

    private static void AssertClean(string text, string reporter)
    {
        text.Should().NotContain(reporter, "quien reportó no aparece en nada que vea la persona reportada");
        text.Should().NotContain(_reportText).And.NotContain(_name);
    }
}
