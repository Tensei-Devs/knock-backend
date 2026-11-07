using System.Net;
using System.Text;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Knock.SecurityTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.SecurityTest.Identity;

/// <summary>Spec 015: <c>POST /api/v1/auth/firebase</c> (público). Host real en Production con MongoDB y Redis reales y el verificador de Firebase doblado.</summary>
public class FirebaseExchangeSecurityTests
{
    private const string _path = "/api/v1/auth/firebase";

    private static object Body(string token, string? deviceId = null, string device = "Teléfono de prueba") =>
        new { firebaseIdToken = token, deviceId = deviceId ?? Guid.NewGuid().ToString("D"), aceptaAviso = true, dispositivo = device };

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("a.b.c")]
    [InlineData("' OR 1=1 --")]
    [InlineData("{\"$ne\":null}")]
    public async Task UnknownOrBrokenFirebaseToken_Returns401TheSameAndCreatesNothing_SEC_015_01(string token)
    {
        var hosts = await SecurityHosts.SharedAsync();
        var users = await hosts.Identity.Users.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

        using var response = await SecureHttp.PostJsonAsync(hosts.IdentityClient, _path, Body(token));

        var raw = await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized, token);
        raw.Should().NotContain("Firebase", "el rechazo no dice qué verificación falló");
        (await hosts.Identity.Users.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(users);
    }

    [Theory]
    [InlineData("""{"deviceId":"0f8e7d6c-5b4a-4392-a1b0-c9d8e7f6a5b4","aceptaAviso":true,"dispositivo":"x"}""")]
    [InlineData("""{"firebaseIdToken":"t","deviceId":"{0f8e7d6c-5b4a-4392-a1b0-c9d8e7f6a5b4}","aceptaAviso":true,"dispositivo":"x"}""")]
    [InlineData("""{"firebaseIdToken":"t","deviceId":"0f8e7d6c5b4a4392a1b0c9d8e7f6a5b4","aceptaAviso":true,"dispositivo":"x"}""")]
    [InlineData("""{"firebaseIdToken":"t","deviceId":"1' OR '1'='1","aceptaAviso":true,"dispositivo":"x"}""")]
    [InlineData("""{"firebaseIdToken":"t","deviceId":"0f8e7d6c-5b4a-4392-a1b0-c9d8e7f6a5b4","aceptaAviso":false,"dispositivo":"x"}""")]
    [InlineData("""{"firebaseIdToken":"t","deviceId":"0f8e7d6c-5b4a-4392-a1b0-c9d8e7f6a5b4","dispositivo":"x"}""")]
    [InlineData("""{"firebaseIdToken":"t","deviceId":"0f8e7d6c-5b4a-4392-a1b0-c9d8e7f6a5b4","aceptaAviso":true,"dispositivo":"   "}""")]
    public async Task InvalidBody_Returns422WithoutCallingFirebase_SEC_015_02(string json)
    {
        var hosts = await SecurityHosts.SharedAsync();
        var calls = hosts.Identity.Firebase.Calls;

        using var response = await SecureHttp.SendRawJsonAsync(hosts.IdentityClient, HttpMethod.Post, _path, json);

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "OR '1'='1");
        hosts.Identity.Firebase.Calls.Should().Be(calls);
    }

    [Theory]
    [InlineData("""{"firebaseIdToken":123,"deviceId":"x","aceptaAviso":true,"dispositivo":"x"}""")]
    [InlineData("""{"firebaseIdToken":{"$ne":null},"deviceId":"x","aceptaAviso":true,"dispositivo":"x"}""")]
    [InlineData("""{"firebaseIdToken":["a"],"deviceId":"x","aceptaAviso":true,"dispositivo":"x"}""")]
    [InlineData("""{"firebaseIdToken":"t","deviceId":"x","aceptaAviso":"yes","dispositivo":"x"}""")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("")]
    public async Task WrongTypesOrBrokenJson_AreRejectedAs4xxNeverAs500_SEC_015_03(string json)
    {
        var hosts = await SecurityHosts.SharedAsync();
        var calls = hosts.Identity.Firebase.Calls;

        using var response = await SecureHttp.SendRawJsonAsync(hosts.IdentityClient, HttpMethod.Post, _path, json);

        ((int)response.StatusCode).Should().BeInRange(400, 499);
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("$ne").And.NotContain("stackTrace");
        hosts.Identity.Firebase.Calls.Should().Be(calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BodyOver8KiB_Returns413_SEC_015_04(bool withoutContentLength)
    {
        var hosts = await SecurityHosts.SharedAsync();
        var json = $$"""{"firebaseIdToken":"{{new string('a', 9000)}}","deviceId":"0f8e7d6c-5b4a-4392-a1b0-c9d8e7f6a5b4","aceptaAviso":true,"dispositivo":"x"}""";
        using HttpContent content = withoutContentLength ? new UnknownLengthContent(json) : new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Post, _path, content);

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.RequestEntityTooLarge);
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/x-www-form-urlencoded")]
    [InlineData("application/xml")]
    public async Task NonJsonContentType_Returns415_SEC_015_05(string contentType)
    {
        var hosts = await SecurityHosts.SharedAsync();
        using var content = new StringContent("firebaseIdToken=x", Encoding.UTF8, contentType);

        using var response = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Post, _path, content);

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task OtherMethods_Return405_SEC_015_06(string method)
    {
        var hosts = await SecurityHosts.SharedAsync();

        using var response = await SecureHttp.SendAsync(hosts.IdentityClient, new HttpMethod(method), _path);

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task ExtraFieldsInTheBody_AreIgnored_TheAccountIsDerivedFromTheVerifiedToken_SEC_015_07()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var phone = SecurityIdentityFactory.NewPhone();
        var uid = "uid-sec-" + Guid.NewGuid().ToString("N");
        var json = $$"""
            {"firebaseIdToken":"{{hosts.Identity.Firebase.Issue(uid, phone)}}","deviceId":"{{Guid.NewGuid():D}}","aceptaAviso":true,"dispositivo":"x",
             "sub":"00000000-0000-8000-8000-000000000001","uid":"admin","celular":"+520000000000","phone":"+520000000000","estado":"suspendida",
             "esTecnico":true,"perfilCompleto":true,"roles":["admin"],"role":"admin","permisos":["*"]}
            """;

        using var response = await SecureHttp.SendRawJsonAsync(hosts.IdentityClient, HttpMethod.Post, _path, json);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await SecureHttp.ReadJsonAsync(response);
        body.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("accessToken", "refreshToken", "esNuevo", "perfilCompleto");
        body.GetProperty("perfilCompleto").GetBoolean().Should().BeFalse();
        var sub = TokenForge.Claim(body.GetProperty("accessToken").GetString()!, "sub");
        sub.Should().NotBe("00000000-0000-8000-8000-000000000001");
        var user = await hosts.Identity.Users.Find(new BsonDocument("sub", sub)).SingleAsync();
        user["estado"].AsString.Should().Be("activa");
        user["esTecnico"].AsBoolean.Should().BeFalse();
        user["perfilCompleto"].AsBoolean.Should().BeFalse();
        user.Names.Should().NotContain(["roles", "role", "permisos", "uid", "celular", "phone"]);
    }

    [Fact]
    public async Task SuccessfulExchange_NeverCachesLeaksNorCompresses_AndIssuesAMinimalJwt_SEC_015_08()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var phone = SecurityIdentityFactory.NewPhone();
        var uid = "uid-sec-" + Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_path, UriKind.Relative))
        {
            Content = System.Net.Http.Json.JsonContent.Create(Body(hosts.Identity.Firebase.Issue(uid, phone))),
        };
        request.Headers.AcceptEncoding.ParseAdd("gzip, br");

        using var response = await hosts.IdentityClient.SendAsync(request);

        SecureHttp.AssertNotCacheable(response);
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain(phone).And.NotContain(phone[3..]).And.NotContain(uid);
        var access = System.Text.Json.JsonElement.Parse(raw).GetProperty("accessToken").GetString()!;
        var payload = System.Text.Json.JsonDocument.Parse(Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(access.Split('.')[1]));
        payload.RootElement.EnumerateObject().Select(p => p.Name).Should().NotContain(["phone", "phone_number", "uid", "role", "roles"]);
        (long.Parse(TokenForge.Claim(access, "exp"), System.Globalization.CultureInfo.InvariantCulture) - long.Parse(TokenForge.Claim(access, "iat"), System.Globalization.CultureInfo.InvariantCulture))
            .Should().Be(900);
    }

    [Fact]
    public async Task SamePhoneWithAnotherUidOrViceVersa_Returns409WithoutRevealingTheOtherAccount_SEC_015_09()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var person = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        var users = await hosts.Identity.Users.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

        using var samePhone = await SecureHttp.PostJsonAsync(hosts.IdentityClient, _path, Body(hosts.Identity.Firebase.Issue("uid-otro-" + Guid.NewGuid().ToString("N"), person.Phone)));
        using var sameUid = await SecureHttp.PostJsonAsync(hosts.IdentityClient, _path, Body(hosts.Identity.Firebase.Issue(person.Uid, SecurityIdentityFactory.NewPhone())));

        var first = await SecureHttp.AssertProblemAsync(samePhone, HttpStatusCode.Conflict, person.Phone, person.Sub, person.Uid, "accessToken");
        var second = await SecureHttp.AssertProblemAsync(sameUid, HttpStatusCode.Conflict, person.Phone, person.Sub, person.Uid, "accessToken");
        SecureHttp.WithoutCorrelationId(first).Should().Be(SecureHttp.WithoutCorrelationId(second), "las dos direcciones del conflicto son indistinguibles");
        (await hosts.Identity.Users.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(users);
    }

    [Fact]
    public async Task SuspendedAccount_Returns403WithoutTokensOrPhone_SEC_015_10()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var person = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        await hosts.Identity.Users.UpdateOneAsync(new BsonDocument("sub", person.Sub), Builders<BsonDocument>.Update.Set("estado", "suspendida"));
        var sessions = await hosts.Identity.Sessions.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

        using var response = await SecureHttp.PostJsonAsync(hosts.IdentityClient, _path, Body(hosts.Identity.Firebase.Issue(person.Uid, person.Phone)));

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Forbidden, person.Phone, person.Sub, "accessToken", "refreshToken");
        (await hosts.Identity.Sessions.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(sessions);
    }

    [Fact]
    public async Task DeviceNameWithInjectionPayloads_IsStoredAsPlainTextAndNeverEchoed_SEC_015_11()
    {
        var hosts = await SecurityHosts.SharedAsync();
        foreach (var name in new[] { "'; DROP TABLE usuarios;--", "{\"$gt\":\"\"}", "<script>alert(1)</script>", "Pixel\r\nSet-Cookie: a=b", "Pixel\0\a" })
        {
            var uid = "uid-sec-" + Guid.NewGuid().ToString("N");
            using var response = await SecureHttp.PostJsonAsync(hosts.IdentityClient, _path, Body(hosts.Identity.Firebase.Issue(uid, SecurityIdentityFactory.NewPhone()), device: name));

            ((int)response.StatusCode).Should().BeLessThan(500);
            (await response.Content.ReadAsStringAsync()).Should().NotContain("DROP TABLE").And.NotContain("<script>");
            response.Headers.Contains("Set-Cookie").Should().BeFalse();
        }

        (await hosts.Identity.Sessions.Find(new BsonDocument("dispositivo", new BsonDocument("$type", "object"))).AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Logs_NeverContainPhoneUidOrTokens_OnAnyOutcome_SEC_015_12()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var phone = SecurityIdentityFactory.NewPhone();
        var uid = "uid-sec-" + Guid.NewGuid().ToString("N");
        var idToken = hosts.Identity.Firebase.Issue(uid, phone);
        var deviceId = Guid.NewGuid().ToString("D");
        var secrets = new List<string> { phone, phone[3..], uid, idToken, deviceId };

        var output = await ConsoleCapture.RunAsync(async () =>
        {
            using var created = await SecureHttp.PostJsonAsync(hosts.IdentityClient, _path, Body(idToken, deviceId));
            var body = await SecureHttp.ReadJsonAsync(created);
            secrets.Add(body.GetProperty("accessToken").GetString()!);
            secrets.Add(body.GetProperty("refreshToken").GetString()!);
            using var again = await SecureHttp.PostJsonAsync(hosts.IdentityClient, _path, Body(idToken, deviceId));
            using var rejected = await SecureHttp.PostJsonAsync(hosts.IdentityClient, _path, Body("token-desconocido-" + uid));
            using var invalid = await SecureHttp.SendRawJsonAsync(hosts.IdentityClient, HttpMethod.Post, _path, "{}");
            using var conflict = await SecureHttp.PostJsonAsync(hosts.IdentityClient, _path, Body(hosts.Identity.Firebase.Issue("otro-" + uid, phone)));
            conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        });

        output.Should().NotBeNullOrWhiteSpace("la operación se registra");
        foreach (var secret in secrets)
        {
            output.Should().NotContain(secret);
        }
    }

    [Fact]
    public async Task AuthRoutesAreRateLimitedPerIpAndCaseAndForwardedHeadersDoNotEvadeIt_SEC_015_13()
    {
        using var factory = await SecurityIdentityFactory.CreateAsync(new Dictionary<string, string?>
        {
            ["RateLimiting:Auth:PermitLimit"] = "3",
            ["RateLimiting:Auth:Window"] = "00:01:00",
        });
        using var client = factory.CreateClient();
        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? limited = null;
        foreach (var (path, spoof) in new[] { (_path, "198.51.100.1"), ("/API/V1/AUTH/firebase", "198.51.100.2"), (_path, "198.51.100.3"), (_path, "198.51.100.4") })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative)) { Content = System.Net.Http.Json.JsonContent.Create(Body("x")) };
            request.Headers.Add(SecurityIdentityFactory.RemoteIpHeader, "203.0.113.50");
            request.Headers.Add("X-Forwarded-For", spoof);
            var response = await client.SendAsync(request);
            statuses.Add(response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                limited = response;
            }
            else
            {
                response.Dispose();
            }
        }

        statuses.Take(3).Should().OnlyContain(s => s == HttpStatusCode.Unauthorized);
        statuses[3].Should().Be(HttpStatusCode.TooManyRequests);
        using (limited)
        {
            var raw = await SecureHttp.AssertProblemAsync(limited!, HttpStatusCode.TooManyRequests, "203.0.113.50", "198.51.100", "PermitLimit");
            raw.Should().NotBeNullOrEmpty();
            int.Parse(limited!.Headers.GetValues("Retry-After").Single(), System.Globalization.CultureInfo.InvariantCulture).Should().BeInRange(1, 60);
            SecureHttp.AssertNotCacheable(limited);
        }

        using var otherIp = await SecureHttp.PostJsonAsync(client, _path, Body("x"), remoteIp: "203.0.113.51");
        otherIp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "otra IP no comparte el contador");
    }
}
