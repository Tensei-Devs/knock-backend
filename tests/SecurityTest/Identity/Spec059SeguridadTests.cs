extern alias IdentityApi;

using System.Net;
using System.Security.Cryptography;
using AwesomeAssertions;
using IdentityApi::Knock.Identity.Api.Modules.Sesiones.Data;
using Knock.IntegrationTest.Support;
using Knock.SecurityTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.SecurityTest.Identity;

/// <summary>
/// Spec 059, T-06: seguridad negativa de la suspensión preventiva por la ruta real del panel (<c>POST /api/v1/admin/reportes/{id}/decision</c>, spec 053) con el host de Identidad en Production, MongoDB y Redis reales.
/// Ningún intento sin permiso, sin sesión del panel válida o con identificadores ajenos cambia una cuenta. El segundo factor se exige al abrir la sesión del panel (spec 033): aquí se prueba que un token que no es de esa
/// sesión (otra audiencia, otra llave, sin <c>pv</c> o <c>sid</c>, versión vieja o sesión cancelada) no decide nada. Solo datos ficticios.
/// </summary>
public sealed class Spec059SeguridadTests
{
    private static readonly object _critical = new { gravedad = "critica", motivo = "peligro inminente" };

    private static Task<SecurityIdentityFactory> HostAsync() => SecurityIdentityFactory.CreateAsync(PanelReportKit.HostSettings);

    private static async Task<string> EstadoAsync(SecurityIdentityFactory factory, ObjectId id) =>
        (await factory.Users.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync())["estado"].AsString;

    private static Task<List<BsonDocument>> SuspensionEventsAsync(SecurityIdentityFactory factory) =>
        factory.Database.GetCollection<BsonDocument>("outbox").Find(Builders<BsonDocument>.Filter.Eq("tipo", "CuentaSuspendida")).ToListAsync();

    private static async Task<string> ReportStateAsync(SecurityIdentityFactory factory, string reportId) =>
        (await factory.Database.GetCollection<BsonDocument>("reportes").Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(reportId))).FirstAsync())["estado"].AsString;

    [Fact]
    public async Task Without_Permission_Without_A_Valid_Panel_Session_Or_Without_A_Token_Nothing_Changes_CA_059_09()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var moderator = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["moderador"]);
        var analyst = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["analista"]);
        var (id, sub) = await PanelReportKit.SeedAccountAsync(factory.Users, technician: true);
        var reportId = await PanelReportKit.SeedReportAsync(factory.Database, Guid.NewGuid().ToString("D"), sub);
        var cancelledSid = ObjectId.GenerateNewId().ToString();
        await factory.Redis.StringSetAsync(RedisSesionCancelada.Key(cancelledSid), "1", TimeSpan.FromMinutes(5));
        var cases = new (string Name, string? Token, HttpStatusCode Status)[]
        {
            ("no-token", null, HttpStatusCode.Unauthorized),
            ("without-the-permission", PanelReportKit.Token(analyst), HttpStatusCode.Forbidden),
            ("app-audience", PanelReportKit.Token(moderator, audience: TokenForge.Audience), HttpStatusCode.Unauthorized),
            ("foreign-key", PanelReportKit.Token(moderator, key: RandomNumberGenerator.GetBytes(32)), HttpStatusCode.Unauthorized),
            ("app-token", TokenForge.Valid(moderator), HttpStatusCode.Unauthorized),
            ("no-permissions-version", PanelReportKit.Token(moderator, permissionsVersion: null), HttpStatusCode.Unauthorized),
            ("no-session", PanelReportKit.Token(moderator, sid: null), HttpStatusCode.Unauthorized),
            ("old-permissions-version", PanelReportKit.Token(moderator, permissionsVersion: 7), HttpStatusCode.Unauthorized),
            ("cancelled-session", PanelReportKit.Token(moderator, cancelledSid), HttpStatusCode.Unauthorized),
        };

        foreach (var (name, token, status) in cases)
        {
            using var response = await PanelReportKit.DecideAsync(client, token, reportId, _critical);
            response.StatusCode.Should().Be(status, name);
            await SecureHttp.AssertProblemAsync(response, status);
        }

        (await EstadoAsync(factory, id)).Should().Be("activa");
        (await SuspensionEventsAsync(factory)).Should().BeEmpty();
        (await ReportStateAsync(factory, reportId)).Should().Be("abierto");
    }

    [Fact]
    public async Task An_Identifier_Of_Another_Person_In_The_Body_Is_Ignored_And_Only_The_Reported_One_Of_The_Report_Is_Suspended_CA_059_10()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var moderator = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["moderador"]);
        var (reportedId, reported) = await PanelReportKit.SeedAccountAsync(factory.Users, technician: true);
        var (bystanderId, bystander) = await PanelReportKit.SeedAccountAsync(factory.Users, technician: true);
        var reportId = await PanelReportKit.SeedReportAsync(factory.Database, Guid.NewGuid().ToString("D"), reported);

        using var response = await PanelReportKit.DecideAsync(
            client,
            PanelReportKit.Token(moderator),
            reportId,
            new { gravedad = "critica", motivo = "peligro inminente", usuarioId = bystander, reportadoId = bystander, sub = bystander, cuentaId = bystanderId.ToString() });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EstadoAsync(factory, reportedId)).Should().Be("suspendida");
        (await EstadoAsync(factory, bystanderId)).Should().Be("activa");
        (await SuspensionEventsAsync(factory)).Should().ContainSingle().Which["carga"]["usuarioId"].AsString.Should().Be(reported);
    }

    [Fact]
    public async Task A_Moderator_Cannot_Decide_A_Report_About_Their_Own_Account_And_Nothing_Changes_CA_059_11()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var (id, sub) = await PanelReportKit.SeedAccountAsync(factory.Users, technician: true);
        await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["moderador"], sub);
        var reportId = await PanelReportKit.SeedReportAsync(factory.Database, Guid.NewGuid().ToString("D"), sub);

        using var response = await PanelReportKit.DecideAsync(client, PanelReportKit.Token(sub), reportId, _critical);

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Forbidden);
        (await EstadoAsync(factory, id)).Should().Be("activa");
        (await SuspensionEventsAsync(factory)).Should().BeEmpty();
        (await ReportStateAsync(factory, reportId)).Should().Be("abierto");
    }
}
