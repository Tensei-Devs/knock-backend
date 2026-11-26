extern alias IdentityApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using IdentityApi::Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;
using IdentityApi::Knock.Identity.Api.Modules.Safety.Services;
using Knock.IntegrationTest.Support;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Identity;

/// <summary>Spec 060: la resolución de la apelación desde el panel y el vencimiento de la suspensión gradual, con MongoDB y Redis reales y el emulador de Firebase.</summary>
public sealed class Spec060ResolucionTests
{
    private static Task<IdentityApiFactory> HostAsync() => IdentityApiFactory.CreateAsync(PanelReportKit.HostSettings);

    private static async Task<HttpResponseMessage> ResolveAsync(HttpClient client, string? token, string reportId, string decision)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/apelaciones/{reportId}/resolucion") { Content = JsonContent.Create(new { decision }) };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request);
    }

    private static async Task<Spec060ApelacionSuspensionTests.Person> AppealedAsync(IdentityApiFactory factory, HttpClient client, string decider, string type = "preventiva", DateTime? expiresAt = null)
    {
        var person = await Spec060ApelacionSuspensionTests.SeedSuspendedAsync(factory, client, decider, type, expiresAt);
        using var appeal = await Spec060ApelacionSuspensionTests.AppealAsync(client, await Spec060ApelacionSuspensionTests.FreshTokenAsync(person.Phone), new { texto = "Pido que revisen la decisión" });
        appeal.EnsureSuccessStatusCode();
        return person;
    }

    private static Task<BsonDocument> UserAsync(IdentityApiFactory factory, ObjectId id) => factory.Users.Find(new BsonDocument("_id", id)).FirstAsync();

    private static Task<List<BsonDocument>> AuditAsync(IdentityApiFactory factory, string reportId) =>
        factory.Database.GetCollection<BsonDocument>("panel_auditoria").Find(new BsonDocument("recursoId", reportId)).ToListAsync();

    [Fact]
    public async Task Accepting_Reactivates_The_Account_Audits_And_Leaves_The_Events_CA_060_07()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var decider = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["moderador"]);
        var reviewer = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["moderador"]);
        var person = await AppealedAsync(factory, client, decider);

        using var response = await ResolveAsync(client, PanelReportKit.Token(reviewer), person.ReportId, "aceptar");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await UserAsync(factory, person.Id);
        user["estado"].AsString.Should().Be("activa");
        user.Contains("suspension").Should().BeFalse();
        (await AuditAsync(factory, person.ReportId)).Select(a => a["evento"].AsString).Should().Contain(["apelacion_aceptada", "cuenta_reactivada"]);
        var outbox = factory.Database.GetCollection<BsonDocument>("outbox");
        (await outbox.CountDocumentsAsync(new BsonDocument("tipo", "CuentaReactivada"))).Should().Be(1);
        (await outbox.CountDocumentsAsync(new BsonDocument("tipo", "ApelacionResuelta"))).Should().Be(1);
        (await factory.Sessions.CountDocumentsAsync(new BsonDocument { ["usuarioId"] = person.Id, ["revocada"] = false })).Should().Be(0, "no se restauran sesiones");
        using var again = await ResolveAsync(client, PanelReportKit.Token(reviewer), person.ReportId, "aceptar");
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Rejecting_Keeps_The_Account_Suspended_And_Blocks_A_New_Appeal_CA_060_08()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var decider = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["moderador"]);
        var reviewer = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["moderador"]);
        var person = await AppealedAsync(factory, client, decider);

        using var response = await ResolveAsync(client, PanelReportKit.Token(reviewer), person.ReportId, "rechazar");
        using var retry = await Spec060ApelacionSuspensionTests.AppealAsync(client, await Spec060ApelacionSuspensionTests.FreshTokenAsync(person.Phone), new { texto = "Pido que revisen la decisión" });
        using var status = await Spec060ApelacionSuspensionTests.AppealAsync(client, await Spec060ApelacionSuspensionTests.FreshTokenAsync(person.Phone), null, method: HttpMethod.Get);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await UserAsync(factory, person.Id))["estado"].AsString.Should().Be("suspendida");
        (await AuditAsync(factory, person.ReportId)).Select(a => a["evento"].AsString).Should().Contain("apelacion_rechazada");
        retry.StatusCode.Should().Be(HttpStatusCode.Conflict, "ninguna apelación nueva sobre la misma suspensión (CA-08a)");
        status.StatusCode.Should().Be(HttpStatusCode.OK);
        (await status.Content.ReadAsStringAsync()).Should().Contain("rechazada").And.NotContain(reviewer);
    }

    [Fact]
    public async Task A_Pending_Appeal_With_Another_Text_Is_Also_409_CA_060_08a()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var person = await AppealedAsync(factory, client, Guid.NewGuid().ToString("D"));

        using var other = await Spec060ApelacionSuspensionTests.AppealAsync(client, await Spec060ApelacionSuspensionTests.FreshTokenAsync(person.Phone), new { texto = "Un texto distinto del anterior" });

        other.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_Gradual_Suspension_Expires_By_Itself_And_A_Critical_One_Never_Does_CA_060_08b()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var gradual = await Spec060ApelacionSuspensionTests.SeedSuspendedAsync(factory, client, Guid.NewGuid().ToString("D"), "gradual", DateTime.UtcNow.AddMinutes(-1));
        var critical = await Spec060ApelacionSuspensionTests.SeedSuspendedAsync(factory, client, Guid.NewGuid().ToString("D"), "preventiva");
        using var scope = factory.Services.CreateScope();
        var handler = scope.ServiceProvider.GetServices<IManejadorDeTarea>().Single(h => h.Tipo == "VencerSuspension");

        await handler.HandleAsync(new TareaReservada(ObjectId.GenerateNewId(), "VencerSuspension", $"{gradual.Sub}:{gradual.ReportId}", "r", 0), CancellationToken.None);
        await handler.HandleAsync(new TareaReservada(ObjectId.GenerateNewId(), "VencerSuspension", $"{critical.Sub}:{critical.ReportId}", "r", 0), CancellationToken.None);
        await handler.HandleAsync(new TareaReservada(ObjectId.GenerateNewId(), "VencerSuspension", $"{gradual.Sub}:{gradual.ReportId}", "r", 0), CancellationToken.None);

        (await UserAsync(factory, gradual.Id))["estado"].AsString.Should().Be("activa");
        (await UserAsync(factory, critical.Id))["estado"].AsString.Should().Be("suspendida");
        (await factory.Database.GetCollection<BsonDocument>("outbox").CountDocumentsAsync(new BsonDocument("tipo", "CuentaReactivada"))).Should().Be(1, "repetir la tarea no duplica");
        (await AuditAsync(factory, gradual.ReportId)).Should().ContainSingle(a => a["evento"].AsString == "cuenta_reactivada");
    }

    [Fact]
    public async Task Suspending_As_Gradual_Schedules_The_Expiry_Task_For_Seven_Days_CA_060_08b()
    {
        await using var factory = await HostAsync();
        var (id, sub) = await PanelReportKit.SeedAccountAsync(factory.Users, technician: true);
        var reportId = ObjectId.GenerateNewId().ToString();

        await factory.Services.GetRequiredService<AccountSuspension>().SuspenderAsync(sub, reportId, Guid.NewGuid().ToString("D"), "gradual", CancellationToken.None);

        var user = await UserAsync(factory, id);
        var since = user["suspension"]["desde"].ToUniversalTime();
        user["suspension"]["venceEn"].ToUniversalTime().Should().BeCloseTo(since.AddDays(7), TimeSpan.FromSeconds(1));
        var task = await factory.Database.GetCollection<BsonDocument>("tareas_programadas").Find(new BsonDocument { ["tipo"] = "VencerSuspension", ["claveNegocio"] = $"{sub}:{reportId}" }).SingleAsync();
        task["programadaPara"].ToUniversalTime().Should().BeCloseTo(since.AddDays(7), TimeSpan.FromSeconds(1));
        await factory.Services.GetRequiredService<AccountSuspension>().SuspenderAsync(sub, ObjectId.GenerateNewId().ToString(), Guid.NewGuid().ToString("D"), "preventiva", CancellationToken.None);
        var after = (await UserAsync(factory, id))["suspension"].AsBsonDocument;
        after["tipo"].AsString.Should().Be("gradual", "ya estaba suspendida: nada cambia");
        after["reporteId"].AsObjectId.ToString().Should().Be(reportId);
        after.Contains("venceEn").Should().BeTrue();
    }

    [Fact]
    public async Task Without_The_Permission_Or_Without_A_Panel_Session_Nothing_Changes_CA_060_09()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var person = await AppealedAsync(factory, client, Guid.NewGuid().ToString("D"));
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);

        using var forbidden = await ResolveAsync(client, PanelReportKit.Token(verifier), person.ReportId, "aceptar");
        using var anonymous = await ResolveAsync(client, null, person.ReportId, "aceptar");

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await UserAsync(factory, person.Id))["estado"].AsString.Should().Be("suspendida");
    }

    [Fact]
    public async Task The_Only_Moderator_Who_Decided_Reviews_It_Marked_As_The_Same_Person_CA_060_10()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var only = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["moderador"]);
        var person = await AppealedAsync(factory, client, only);

        using var response = await ResolveAsync(client, PanelReportKit.Token(only), person.ReportId, "aceptar");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await factory.Database.GetCollection<BsonDocument>("reportes").Find(new BsonDocument("_id", ObjectId.Parse(person.ReportId))).FirstAsync();
        report["apelacion"]["resuelta"]["mismaPersona"].AsBoolean.Should().BeTrue();
    }

    [Fact]
    public async Task The_Moderator_Who_Decided_Cannot_Review_It_When_Another_Reviewer_Exists_CA_060_10()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var decider = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["moderador"]);
        await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["administrador"]);
        var person = await AppealedAsync(factory, client, decider);

        using var response = await ResolveAsync(client, PanelReportKit.Token(decider), person.ReportId, "aceptar");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await UserAsync(factory, person.Id))["estado"].AsString.Should().Be("suspendida");
    }

    [Fact]
    public async Task An_Unknown_Or_Not_Suspension_Appeal_Is_404_And_A_Bad_Decision_Is_422_CA_060_09()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var reviewer = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["moderador"]);
        var open = await PanelReportKit.SeedReportAsync(factory.Database, Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"));

        using var missing = await ResolveAsync(client, PanelReportKit.Token(reviewer), ObjectId.GenerateNewId().ToString(), "aceptar");
        using var notAppeal = await ResolveAsync(client, PanelReportKit.Token(reviewer), open, "aceptar");
        using var invalid = await ResolveAsync(client, PanelReportKit.Token(reviewer), open, "quizás");

        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        notAppeal.StatusCode.Should().Be(HttpStatusCode.NotFound);
        invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
