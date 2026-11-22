extern alias IdentityApi;

using System.Net;
using System.Security.Cryptography;
using AwesomeAssertions;
using IdentityApi::Knock.Identity.Api.Modules.Panel.Data;
using IdentityApi::Knock.Identity.Api.Modules.Safety.Services;
using IdentityApi::Knock.Identity.Api.Modules.Sesiones.Data;
using Knock.IntegrationTest.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Identity;

/// <summary>
/// Spec 059, T-06 (segunda parte): las suspensiones disparadas por la ruta real del panel, <c>POST /api/v1/admin/reportes/{id}/decision</c> (spec 053, REP-05), con MongoDB y Redis reales: renovación en vuelo,
/// sesión del panel, reportes sin decidir, regla gradual, Redis caído y apelación pendiente. Los valores al azar se comparan por campo, nunca como texto dentro de un cuerpo con ids al azar.
/// </summary>
public sealed partial class Spec059SuspensionTests
{
    private static readonly Uri _refreshUri = new("/api/v1/auth/refresh", UriKind.Relative);

    private static async Task<(string Sub, string Token)> StartPanelAsync(IdentityApiFactory factory)
    {
        var moderator = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["moderador"]);
        return (moderator, PanelReportKit.Token(moderator));
    }

    private static Task<IdentityApiFactory> HostAsync(Dictionary<string, string?>? extra = null, Action<IServiceCollection>? services = null)
    {
        var settings = PanelReportKit.HostSettings.ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var (key, value) in extra ?? [])
        {
            settings[key] = value;
        }

        return IdentityApiFactory.CreateAsync(settings, services);
    }

    private static async Task<(string Token, string Fingerprint)> SeedRefreshableSessionAsync(IdentityApiFactory factory, ObjectId user, string sub)
    {
        var raw = RandomNumberGenerator.GetBytes(32);
        await factory.Sessions.InsertOneAsync(new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() }, { "usuarioId", user }, { "sub", sub }, { "dispositivo", "Teléfono de prueba" }, { "revocada", false },
            { "huellaRenovacion", new BsonBinaryData(SHA256.HashData(raw)) }, { "huellasUsadas", new BsonArray() },
            { "ultimaActividad", new BsonDateTime(DateTime.UtcNow) }, { "venceEn", new BsonDateTime(DateTime.UtcNow.AddDays(30)) },
        });
        return (Base64UrlEncoder.Encode(raw), Convert.ToHexString(SHA256.HashData(raw)));
    }

    private static async Task<(HttpStatusCode Status, string? Refresh)> RefreshAsync(HttpClient client, string refreshToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _refreshUri) { Content = System.Net.Http.Json.JsonContent.Create(new { refreshToken }) };
        using var response = await client.SendAsync(request);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return (response.StatusCode, null);
        }

        using var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (response.StatusCode, body.RootElement.GetProperty("refreshToken").GetString());
    }

    /// <summary>La decisión del panel y varias renovaciones lanzadas a la vez; espera a todas antes de devolver.</summary>
    private static async Task<(HttpResponseMessage Decision, List<(HttpStatusCode Status, string? Refresh)> Renewals)> RaceAsync(HttpClient client, string token, string reportId, string[] refreshTokens)
    {
        var decision = PanelReportKit.DecideAsync(client, token, reportId, new { gravedad = "critica", motivo = "peligro inminente" });
        var renewals = refreshTokens.Select(refreshToken => RefreshAsync(client, refreshToken)).ToList();
        await Task.WhenAll([decision, .. renewals]);
        return (await decision, [.. renewals.Select(task => task.Result)]);
    }

    private static async Task<string> UserStateAsync(IdentityApiFactory factory, ObjectId id) =>
        (await factory.Users.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync())["estado"].AsString;

    [Fact]
    public async Task A_Renewal_In_Flight_Leaves_No_Live_Session_Nor_A_Valid_Refresh_Token_After_The_Panel_Decision_CA_059_07()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var (_, token) = await StartPanelAsync(factory);
        var (id, sub) = await SeedAccountAsync(factory, technician: true);
        var first = await SeedRefreshableSessionAsync(factory, id, sub);
        var second = await SeedRefreshableSessionAsync(factory, id, sub);
        var reportId = await PanelReportKit.SeedReportAsync(factory.Database, Guid.NewGuid().ToString("D"), sub);

        var (decided, renewals) = await RaceAsync(client, token, reportId, [first.Token, second.Token, first.Token]);
        using var _ = decided;

        decided.StatusCode.Should().Be(HttpStatusCode.OK);
        var known = new[] { first.Token, second.Token }.Concat(renewals.Select(r => r.Refresh).OfType<string>()).ToList();
        foreach (var refreshToken in known)
        {
            (await RefreshAsync(client, refreshToken)).Status.Should().Be(HttpStatusCode.Unauthorized);
        }

        (await UserStateAsync(factory, id)).Should().Be("suspendida");
        (await factory.Sessions.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("usuarioId", id) & Builders<BsonDocument>.Filter.Eq("revocada", false))).Should().Be(0);
    }

    [Fact]
    public async Task The_Panel_Session_Of_A_Suspended_Technician_Is_Revoked_And_Cannot_Be_Renewed_CA_059_08()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var (_, token) = await StartPanelAsync(factory);
        var (_, technicianSub) = await SeedAccountAsync(factory, technician: true);
        await factory.Services.GetRequiredService<IPanelCuentasStore>().InsertAsync(new NuevaCuentaPanel(technicianSub, "Cuenta doble", ["verificador"], DateTime.UtcNow), CancellationToken.None);
        var sessions = factory.Services.GetRequiredService<IPanelSesionesStore>();
        var panelSid = ObjectId.GenerateNewId().ToString();
        var fingerprint = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var now = DateTime.UtcNow;
        await sessions.CreateAsync(new NuevaSesionPanel(panelSid, technicianSub, fingerprint, 1, now, now.AddMinutes(20), now.AddHours(8)), CancellationToken.None);
        var reportId = await PanelReportKit.SeedReportAsync(factory.Database, Guid.NewGuid().ToString("D"), technicianSub);

        using var decided = await PanelReportKit.DecideAsync(client, token, reportId, new { gravedad = "critica", motivo = "peligro inminente" });

        decided.StatusCode.Should().Be(HttpStatusCode.OK);
        (await sessions.FindByIdAsync(panelSid, CancellationToken.None))!.Revocada.Should().BeTrue();
        (await sessions.FindByFingerprintAsync(fingerprint, CancellationToken.None))!.Revocada.Should().BeTrue("la renovación del panel la encuentra revocada");
    }

    [Fact]
    public async Task Twenty_Open_Reports_From_Twenty_People_And_A_Lighter_Decision_Do_Not_Suspend_CA_059_12()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var (_, token) = await StartPanelAsync(factory);
        var (id, sub) = await SeedAccountAsync(factory, technician: true);
        var reports = new List<string>();
        for (var i = 0; i < 20; i++)
        {
            reports.Add(await PanelReportKit.SeedReportAsync(factory.Database, Guid.NewGuid().ToString("D"), sub));
        }

        using var queue = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/reportes?limite=50") { Headers = { Authorization = new("Bearer", token) } });
        using var lighter = await PanelReportKit.DecideAsync(client, token, reports[0], new { gravedad = "baja", motivo = "mediación acordada" });

        queue.StatusCode.Should().Be(HttpStatusCode.OK);
        lighter.StatusCode.Should().Be(HttpStatusCode.OK);
        (await UserStateAsync(factory, id)).Should().Be("activa");
        (await OutboxAsync(factory, "CuentaSuspendida")).Should().BeEmpty();
    }

    [Fact]
    public async Task The_Third_Valid_Warning_Suspends_As_Gradual_In_The_Same_Transaction_And_Fewer_Do_Not_CA_059_13()
    {
        await using var factory = await HostAsync(new Dictionary<string, string?> { ["Suspension:UmbralAdvertencias"] = "3" });
        using var client = factory.CreateClient();
        var (_, token) = await StartPanelAsync(factory);
        var (id, sub) = await SeedAccountAsync(factory, technician: true);
        var reports = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            reports.Add(await PanelReportKit.SeedReportAsync(factory.Database, Guid.NewGuid().ToString("D"), sub));
        }

        foreach (var (report, severity) in new[] { (reports[0], "media"), (reports[1], "baja"), (reports[2], "media") })
        {
            using var response = await PanelReportKit.DecideAsync(client, token, report, new { gravedad = severity, motivo = "advertencia formal" });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await UserStateAsync(factory, id)).Should().Be("activa", "dos advertencias vigentes y una mediación");
        using var third = await PanelReportKit.DecideAsync(client, token, reports[3], new { gravedad = "media", motivo = "advertencia formal" });

        third.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await factory.Users.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync();
        user["estado"].AsString.Should().Be("suspendida");
        user["suspension"]["tipo"].AsString.Should().Be("gradual");
        user["suspension"]["reporteId"].AsObjectId.ToString().Should().Be(reports[3]);
        (await OutboxAsync(factory, "CuentaSuspendida")).Should().ContainSingle();
    }

    [Fact]
    public async Task Without_The_Threshold_Any_Number_Of_Warnings_Never_Suspends_CA_059_13()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var (_, token) = await StartPanelAsync(factory);
        var (id, sub) = await SeedAccountAsync(factory, technician: true);

        for (var i = 0; i < 4; i++)
        {
            var report = await PanelReportKit.SeedReportAsync(factory.Database, Guid.NewGuid().ToString("D"), sub);
            using var response = await PanelReportKit.DecideAsync(client, token, report, new { gravedad = "media", motivo = "advertencia formal" });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await UserStateAsync(factory, id)).Should().Be("activa");
    }

    [Fact]
    public async Task A_Failing_Redis_Does_Not_Revert_The_Suspension_And_Renewal_Stays_Closed_By_The_Database_CA_059_18()
    {
        await using var factory = await HostAsync(services: collection => collection.AddSingleton<ISesionCancelada>(new FailingCancelledSessions()));
        using var client = factory.CreateClient();
        var (_, token) = await StartPanelAsync(factory);
        var (id, sub) = await SeedAccountAsync(factory, technician: true);
        var session = await SeedRefreshableSessionAsync(factory, id, sub);
        var reportId = await PanelReportKit.SeedReportAsync(factory.Database, Guid.NewGuid().ToString("D"), sub);

        using var decided = await PanelReportKit.DecideAsync(client, token, reportId, new { gravedad = "critica", motivo = "peligro inminente" });

        decided.StatusCode.Should().Be(HttpStatusCode.OK);
        (await UserStateAsync(factory, id)).Should().Be("suspendida");
        (await RefreshAsync(client, session.Token)).Status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_Appeal_Does_Not_Reactivate_And_Only_Reactivar_Restores_The_Account_CA_059_19()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var (moderator, token) = await StartPanelAsync(factory);
        var (id, sub) = await SeedAccountAsync(factory, technician: true);
        var reportId = await PanelReportKit.SeedReportAsync(factory.Database, Guid.NewGuid().ToString("D"), sub);
        using var decided = await PanelReportKit.DecideAsync(client, token, reportId, new { gravedad = "critica", motivo = "peligro inminente" });
        decided.StatusCode.Should().Be(HttpStatusCode.OK);

        await factory.Database.GetCollection<BsonDocument>("reportes").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(reportId)),
            Builders<BsonDocument>.Update.Set("estado", "en_apelacion").Set("apelacion", new BsonDocument { ["texto"] = "Apelación ficticia" }));

        (await UserStateAsync(factory, id)).Should().Be("suspendida");
        (await OutboxAsync(factory, "CuentaReactivada")).Should().BeEmpty();
        await factory.Services.GetRequiredService<AccountSuspension>().ReactivarAsync(sub, reportId, moderator, CancellationToken.None);
        (await UserStateAsync(factory, id)).Should().Be("activa");
    }

    private sealed class FailingCancelledSessions : ISesionCancelada
    {
        public Task CancelAsync(string sessionId, TimeSpan lifetime, CancellationToken cancellationToken) => throw new InvalidOperationException("Redis is down.");
    }
}
