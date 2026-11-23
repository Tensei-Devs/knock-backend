extern alias IdentityApi;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using AwesomeAssertions;
using IdentityApi::Knock.Identity.Api.Modules.Safety.Services;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Identity;

/// <summary>
/// Spec 060: la apelación pública de una persona suspendida y su resolución desde el panel, con el emulador de Firebase, MongoDB y Redis reales. Solo celulares y datos ficticios;
/// los identificadores al azar se comparan por campo.
/// </summary>
public sealed class Spec060ApelacionSuspensionTests
{
    private const string _path = "/api/v1/suspension/apelacion";
    private const string _text = "Creo que hubo un error en la decisión";

    private static string NewPhone() => "+5255" + RandomNumberGenerator.GetInt32(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);

    internal sealed record Person(ObjectId Id, string Sub, string Phone, string ReportId);

    private static Task<IdentityApiFactory> HostAsync(Dictionary<string, string?>? extra = null)
    {
        var settings = PanelReportKit.HostSettings.ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var (key, value) in extra ?? [])
        {
            settings[key] = value;
        }

        return IdentityApiFactory.CreateAsync(settings);
    }

    /// <summary>Crea la cuenta por el canje real, la deja <c>suspendida</c> por un reporte ya decidido por <paramref name="decider"/> y devuelve a la persona.</summary>
    internal static async Task<Person> SeedSuspendedAsync(IdentityApiFactory factory, HttpClient client, string decider, string type = "preventiva", DateTime? expiresAt = null, string? appealText = null)
    {
        var phone = NewPhone();
        var (idToken, _) = await FirebaseEmulator.SignInWithPhoneAsync(phone);
        (await AuthHttp.ExchangeAsync(client, idToken, remoteIp: RandomIp())).EnsureSuccessStatusCode();
        var user = await factory.Users.Find(new BsonDocument("sub", new BsonDocument("$exists", true))).SortByDescending(u => u["_id"]).FirstAsync();
        var reportId = await PanelReportKit.SeedReportAsync(factory.Database, Guid.NewGuid().ToString("D"), user["sub"].AsString, state: "decidido", severity: "critica");
        var reports = factory.Database.GetCollection<BsonDocument>("reportes");
        var set = new BsonDocument
        {
            ["decision"] = new BsonDocument { ["gravedad"] = "critica", ["respuesta"] = "suspension_preventiva", ["motivo"] = "peligro", ["decididoPor"] = decider, ["decididoEn"] = new BsonDateTime(DateTime.UtcNow) },
        };
        if (appealText is not null)
        {
            set["apelacion"] = new BsonDocument { ["texto"] = appealText, ["creadoEn"] = new BsonDateTime(DateTime.UtcNow), ["origen"] = "sesion" };
        }

        await reports.UpdateOneAsync(new BsonDocument("_id", ObjectId.Parse(reportId)), new BsonDocument("$set", set));
        var suspension = new BsonDocument { ["tipo"] = type, ["reporteId"] = ObjectId.Parse(reportId), ["desde"] = new BsonDateTime(DateTime.UtcNow) };
        if (expiresAt is { } due)
        {
            suspension["venceEn"] = new BsonDateTime(due);
        }

        await factory.Users.UpdateOneAsync(new BsonDocument("_id", user["_id"]), new BsonDocument("$set", new BsonDocument { ["estado"] = "suspendida", ["suspension"] = suspension }));
        return new Person(user["_id"].AsObjectId, user["sub"].AsString, phone, reportId);
    }

    internal static string RandomIp() => "10." + string.Join('.', RandomNumberGenerator.GetBytes(3).Select(b => b.ToString(CultureInfo.InvariantCulture)));

    internal static async Task<string> FreshTokenAsync(string phone) => (await FirebaseEmulator.SignInWithPhoneAsync(phone)).IdToken;

    internal static async Task<HttpResponseMessage> AppealAsync(HttpClient client, string? idToken, object? body, string? ip = null, HttpMethod? method = null)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Post, _path);
        if (idToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Add(IdentityApiFactory.RemoteIpHeader, ip ?? RandomIp());
        return await client.SendAsync(request);
    }

    private static async Task<string> StateOfAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("estado").GetString()!;
    }

    [Fact]
    public async Task A_Suspended_Person_With_A_Recent_Code_Appeals_Without_Opening_A_Session_CA_060_01()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var person = await SeedSuspendedAsync(factory, client, Guid.NewGuid().ToString("D"));
        var sessionsBefore = await factory.Sessions.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

        using var response = await AppealAsync(client, await FreshTokenAsync(person.Phone), new { texto = _text });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StateOfAsync(response)).Should().Be("pendiente");
        (await response.Content.ReadAsStringAsync()).Should().NotContain(_text).And.NotContain(person.Phone);
        (await factory.Users.Find(new BsonDocument("_id", person.Id)).FirstAsync())["estado"].AsString.Should().Be("suspendida");
        (await factory.Sessions.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(sessionsBefore, "no se emite ninguna sesión");
        var report = await factory.Database.GetCollection<BsonDocument>("reportes").Find(new BsonDocument("_id", ObjectId.Parse(person.ReportId))).FirstAsync();
        report["estado"].AsString.Should().Be("en_apelacion");
        report["apelacion"]["origen"].AsString.Should().Be("suspension");
    }

    [Fact]
    public async Task A_Token_That_Is_Invalid_Old_Or_From_Another_Project_Gets_401_And_Changes_Nothing_CA_060_02()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var person = await SeedSuspendedAsync(factory, client, Guid.NewGuid().ToString("D"));
        var now = DateTimeOffset.UtcNow;
        var old = FirebaseEmulator.ForgeUnsignedIdToken("uid-" + Guid.NewGuid().ToString("N"), person.Phone, FirebaseEmulator.ProjectId, now.AddMinutes(-30), now.AddMinutes(30));
        var otherProject = FirebaseEmulator.ForgeUnsignedIdToken("uid-" + Guid.NewGuid().ToString("N"), person.Phone, "otro-proyecto", now.AddMinutes(-1), now.AddMinutes(30));
        var expired = FirebaseEmulator.ForgeUnsignedIdToken("uid-" + Guid.NewGuid().ToString("N"), person.Phone, FirebaseEmulator.ProjectId, now.AddHours(-3), now.AddHours(-2));

        foreach (var token in new[] { "no-es-un-token", old, otherProject, expired })
        {
            using var response = await AppealAsync(client, token, new { texto = _text });
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        using var without = await AppealAsync(client, null, new { texto = _text });
        without.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await factory.Database.GetCollection<BsonDocument>("reportes").Find(new BsonDocument("_id", ObjectId.Parse(person.ReportId))).FirstAsync()).Contains("apelacion").Should().BeFalse();
    }

    [Fact]
    public async Task Unknown_Active_And_Not_Appealable_Numbers_Get_The_Same_404_CA_060_03()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var decider = Guid.NewGuid().ToString("D");
        var active = await SeedSuspendedAsync(factory, client, decider);
        await factory.Users.UpdateOneAsync(new BsonDocument("_id", active.Id), new BsonDocument("$set", new BsonDocument("estado", "activa")));
        var noDecision = await SeedSuspendedAsync(factory, client, decider);
        await factory.Database.GetCollection<BsonDocument>("reportes").UpdateOneAsync(
            new BsonDocument("_id", ObjectId.Parse(noDecision.ReportId)),
            new BsonDocument("$set", new BsonDocument("estado", "abierto")));
        var bodies = new List<(HttpStatusCode Status, string Code)>();

        foreach (var token in new[] { await FreshTokenAsync(NewPhone()), await FreshTokenAsync(active.Phone), await FreshTokenAsync(noDecision.Phone) })
        {
            using var response = await AppealAsync(client, token, new { texto = _text });
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            bodies.Add((response.StatusCode, body.RootElement.GetProperty("code").GetString() ?? string.Empty));
        }

        bodies.Should().OnlyContain(b => b.Status == HttpStatusCode.NotFound && b.Code == "not_found");
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData(null)]
    public async Task A_Text_Out_Of_Range_Gets_422_Without_Repeating_It_CA_060_04(string? text)
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var person = await SeedSuspendedAsync(factory, client, Guid.NewGuid().ToString("D"));

        using var response = await AppealAsync(client, await FreshTokenAsync(person.Phone), new { texto = text });
        using var long501 = await AppealAsync(client, await FreshTokenAsync(person.Phone), new { texto = new string('a', 501) });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        long501.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await long501.Content.ReadAsStringAsync()).Should().Contain("texto").And.NotContain(new string('a', 501));
    }

    [Fact]
    public async Task Repeating_With_The_Same_Text_Is_Idempotent_And_With_Another_Text_Is_409_CA_060_05()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var person = await SeedSuspendedAsync(factory, client, Guid.NewGuid().ToString("D"));

        using var first = await AppealAsync(client, await FreshTokenAsync(person.Phone), new { texto = _text });
        using var again = await AppealAsync(client, await FreshTokenAsync(person.Phone), new { texto = _text });
        using var other = await AppealAsync(client, await FreshTokenAsync(person.Phone), new { texto = _text + " otra vez" });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        other.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await other.Content.ReadAsStringAsync()).Should().Contain("appeal_not_allowed");
    }

    [Fact]
    public async Task Simultaneous_Different_Appeals_Leave_One_Winner_CA_060_05()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var person = await SeedSuspendedAsync(factory, client, Guid.NewGuid().ToString("D"));
        var tokens = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            tokens.Add(await FreshTokenAsync(person.Phone));
        }

        var responses = await Task.WhenAll(tokens.Select((token, i) => AppealAsync(client, token, new { texto = $"{_text} versión {i}" })));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(2);
    }

    [Fact]
    public async Task A_Pending_Appeal_Keeps_The_Account_Suspended_And_The_Status_Is_Readable_CA_060_06()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var person = await SeedSuspendedAsync(factory, client, Guid.NewGuid().ToString("D"));
        (await AppealAsync(client, await FreshTokenAsync(person.Phone), new { texto = _text })).EnsureSuccessStatusCode();

        using var status = await AppealAsync(client, await FreshTokenAsync(person.Phone), null, method: HttpMethod.Get);

        status.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StateOfAsync(status)).Should().Be("pendiente");
        (await factory.Users.Find(new BsonDocument("_id", person.Id)).FirstAsync())["estado"].AsString.Should().Be("suspendida");
    }

    [Fact]
    public async Task The_Sixth_Appeal_Of_An_Hour_For_The_Same_Number_Gets_429_CA_060_11()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var person = await SeedSuspendedAsync(factory, client, Guid.NewGuid().ToString("D"));
        for (var i = 0; i < SuspensionAppealLimiter.PermitLimit; i++)
        {
            using var allowed = await AppealAsync(client, await FreshTokenAsync(person.Phone), new { texto = _text });
            allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var limited = await AppealAsync(client, await FreshTokenAsync(person.Phone), new { texto = _text });

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.Contains("Retry-After").Should().BeTrue();
    }

    [Fact]
    public async Task The_Twenty_First_Request_From_The_Same_Ip_Gets_429_CA_060_11()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var ip = RandomIp();
        for (var i = 0; i < 20; i++)
        {
            using var allowed = await AppealAsync(client, "no-es-un-token", new { texto = _text }, ip);
            allowed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        using var limited = await AppealAsync(client, "no-es-un-token", new { texto = _text }, ip);

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Responses_And_Records_Never_Carry_The_Token_The_Number_Or_The_Text_CA_060_12()
    {
        await using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var person = await SeedSuspendedAsync(factory, client, Guid.NewGuid().ToString("D"));
        var token = await FreshTokenAsync(person.Phone);

        using var ok = await AppealAsync(client, token, new { texto = _text });
        using var bad = await AppealAsync(client, token, new { texto = _text + " distinto" });
        using var invalid = await AppealAsync(client, token, new { texto = "corto" });

        foreach (var response in new[] { ok, bad, invalid })
        {
            var body = await response.Content.ReadAsStringAsync();
            body.Should().NotContain(token).And.NotContain(person.Phone).And.NotContain(_text).And.NotContain(person.Sub);
        }

        var audit = await factory.Database.GetCollection<BsonDocument>("panel_auditoria").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        audit.Select(a => a.ToJson()).Should().NotContain(json => json.Contains(_text, StringComparison.Ordinal) || json.Contains(person.Phone, StringComparison.Ordinal));
    }
}
