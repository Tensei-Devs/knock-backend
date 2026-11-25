using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using static Knock.IntegrationTest.Support.PanelReportKit;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Identity;

/// <summary>
/// Spec 053, VER-03 a VER-05 (T-02, T-03 y T-04) por las rutas reales del panel, con MongoDB y Redis reales: la cola de verificaciones, la apertura con enlaces de 5 minutos y su auditoría, y la decisión que destruye la
/// llave de las fotos, deja la auditoría en la misma transacción y no se duplica por reintentos. El almacén de fotos del host de pruebas solo emite enlaces (sin Blob real). Solo datos ficticios;
/// los identificadores de las personas son aleatorios y se comparan por campo, nunca como texto dentro de un cuerpo.
/// </summary>
public sealed class Spec053VerificacionesPanelTests
{
    private const string _route = "/api/v1/admin/verificaciones";
    private const string _reason = "Fotos legibles y datos coincidentes";

    private static Task<IdentityApiFactory> HostAsync() => IdentityApiFactory.CreateAsync(PanelReportKit.HostSettings);

    private static IMongoCollection<BsonDocument> Verifications(IdentityApiFactory factory) => factory.Database.GetCollection<BsonDocument>("verificaciones");

    private static IMongoCollection<BsonDocument> Audit(IdentityApiFactory factory) => factory.Database.GetCollection<BsonDocument>("panel_auditoria");

    private static async Task<ObjectId> SeedAsync(IdentityApiFactory factory, DateTime? sentAt = null, string state = "en_revision", string? ownerSub = null)
    {
        var id = ObjectId.GenerateNewId();
        await Verifications(factory).InsertOneAsync(new BsonDocument
        {
            { "_id", id },
            { "usuarioId", ownerSub ?? Guid.NewGuid().ToString("D") },
            { "nivel", "basica" },
            { "estado", state },
            { "curpHmac", Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)) },
            { "curpVigente", true },
            { "llaveEnvuelta", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) },
            { "llaveKekVersion", "test-kek" },
            {
                "fotos", new BsonArray
                {
                    new BsonDocument { { "tipo", "ine_frente" }, { "estado", "cifrada" }, { "blob", "f/" + id + "/ine_frente" } },
                    new BsonDocument { { "tipo", "selfie" }, { "estado", "cifrada" }, { "blob", "f/" + id + "/selfie" } },
                }
            },
            { "enviadoEn", new BsonDateTime(sentAt ?? DateTime.UtcNow.AddHours(-1)) },
            { "creadoEn", new BsonDateTime(DateTime.UtcNow.AddDays(-1)) },
        });
        return id;
    }

    private static Task<List<BsonDocument>> AuditOfAsync(IdentityApiFactory factory, ObjectId id, string name) =>
        Audit(factory).Find(Builders<BsonDocument>.Filter.Eq("recursoId", id.ToString()) & Builders<BsonDocument>.Filter.Eq("evento", name)).ToListAsync();

    [Fact]
    public async Task TheQueue_ListsUnderReviewOldestFirst_WithinThirtyDays_WithoutPhotosOrPersonalData_CA_053_30()
    {
        using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);
        var now = DateTime.UtcNow;
        var newest = await SeedAsync(factory, now.AddHours(-1));
        var oldest = await SeedAsync(factory, now.AddDays(-10));
        var middle = await SeedAsync(factory, now.AddDays(-2));
        await SeedAsync(factory, now.AddHours(-3), "verificada");
        await SeedAsync(factory, now.AddDays(-45));

        using var response = await SendAsync(client, HttpMethod.Get, _route, PanelReportKit.Token(verifier));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await JsonAsync(response)).GetProperty("items").EnumerateArray().ToList();
        items.Select(item => item.GetProperty("id").GetString()).Should().Equal(oldest.ToString(), middle.ToString(), newest.ToString());
        items.Should().OnlyContain(item => item.GetProperty("estado").GetString() == "en_revision" && item.GetProperty("tipo").GetString() == "identidad");
        items.SelectMany(item => item.EnumerateObject().Select(property => property.Name)).Distinct().Should().BeEquivalentTo("id", "tipo", "nivel", "estado", "enviadaEn");
    }

    [Fact]
    public async Task TheQueue_PagesByCursorWithoutRepeatingOrSkipping_CA_053_05()
    {
        using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);
        var ids = new List<string>();
        for (var index = 0; index < 5; index++)
        {
            ids.Add((await SeedAsync(factory, DateTime.UtcNow.AddHours(-10 + index))).ToString());
        }

        using var first = await SendAsync(client, HttpMethod.Get, _route + "?limite=3", PanelReportKit.Token(verifier));
        var firstPage = await JsonAsync(first);
        var cursor = firstPage.GetProperty("nextCursor").GetString();
        using var second = await SendAsync(client, HttpMethod.Get, _route + "?limite=3&cursor=" + Uri.EscapeDataString(cursor!), PanelReportKit.Token(verifier));
        var secondPage = await JsonAsync(second);

        firstPage.GetProperty("items").GetArrayLength().Should().Be(3);
        secondPage.GetProperty("items").GetArrayLength().Should().Be(2);
        secondPage.TryGetProperty("nextCursor", out _).Should().BeFalse("en la última página el cursor no se envía (el campo nulo se omite, como en las demás listas)");
        firstPage.GetProperty("items").EnumerateArray().Concat(secondPage.GetProperty("items").EnumerateArray()).Select(item => item.GetProperty("id").GetString()).Should().Equal(ids);
    }

    [Fact]
    public async Task Opening_ReturnsTheFiveMinuteLinks_AndAuditsWhoOpenedWithoutTheLinks_CA_053_06_CA_053_31()
    {
        using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);
        var id = await SeedAsync(factory);
        var before = DateTime.UtcNow;

        using var response = await SendAsync(client, HttpMethod.Get, _route + "/" + id, PanelReportKit.Token(verifier));

        var after = DateTime.UtcNow;
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var detail = await JsonAsync(response);
        detail.GetProperty("id").GetString().Should().Be(id.ToString());
        detail.GetProperty("estado").GetString().Should().Be("en_revision");
        var photos = detail.GetProperty("fotos").EnumerateArray().ToList();
        photos.Select(photo => photo.GetProperty("tipo").GetString()).Should().Equal("ine_frente", "selfie");
        photos.Should().OnlyContain(photo => Uri.IsWellFormedUriString(photo.GetProperty("url").GetString(), UriKind.Absolute));
        photos.Select(photo => photo.GetProperty("venceEn").GetDateTime().ToUniversalTime()).Should().OnlyContain(expires => expires >= before.AddMinutes(5) && expires <= after.AddMinutes(5));
        var entries = await AuditOfAsync(factory, id, "verificacion_abierta");
        entries.Should().ContainSingle();
        entries[0]["sub"].AsString.Should().Be(verifier);
        entries[0].ToJson().Should().NotContain(photos[0].GetProperty("url").GetString()!, "los enlaces no se registran");
    }

    [Fact]
    public async Task Opening_AnUnknownOrAlreadyDecidedVerification_Answers404Or410AndLeavesNoOpeningRecord_CA_053_10_CA_053_11()
    {
        using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);
        var decided = await SeedAsync(factory, state: "verificada");

        using var unknown = await SendAsync(client, HttpMethod.Get, _route + "/" + ObjectId.GenerateNewId(), PanelReportKit.Token(verifier));
        using var gone = await SendAsync(client, HttpMethod.Get, _route + "/" + decided, PanelReportKit.Token(verifier));

        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await JsonAsync(unknown)).GetProperty("code").GetString().Should().Be("verification_not_found");
        gone.StatusCode.Should().Be(HttpStatusCode.Gone);
        (await JsonAsync(gone)).GetProperty("code").GetString().Should().Be("photos_destroyed");
        (await Audit(factory).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("evento", "verificacion_abierta"))).Should().Be(0);
    }

    [Fact]
    public async Task Approving_Answers200_DestroysTheKeyAndThePhotos_AndAuditsInTheSameWrite_CA_053_12_CA_053_32()
    {
        using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);
        var id = await SeedAsync(factory);

        using var response = await SendAsync(client, HttpMethod.Post, _route + "/" + id + "/decision", PanelReportKit.Token(verifier), new { decision = "aprobada" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await JsonAsync(response);
        result.GetProperty("id").GetString().Should().Be(id.ToString());
        result.GetProperty("estado").GetString().Should().Be("verificada");
        var stored = await Verifications(factory).Find(Builders<BsonDocument>.Filter.Eq("_id", id)).SingleAsync();
        stored["estado"].AsString.Should().Be("verificada");
        stored["decididoPor"].AsString.Should().Be(verifier);
        stored["nivelOtorgado"].AsString.Should().Be("basica");
        stored["llaveEnvuelta"].IsBsonNull.Should().BeTrue("la llave se destruye al decidir");
        stored["fotos"].AsBsonArray.Should().OnlyContain(photo => photo["estado"] == "borrada" && photo["blob"].IsBsonNull);
        var entries = await AuditOfAsync(factory, id, "verificacion_decidida");
        entries.Should().ContainSingle();
        entries[0]["sub"].AsString.Should().Be(verifier);
        entries[0]["resultado"].AsString.Should().Be("aprobada");
    }

    [Fact]
    public async Task Rejecting_RequiresAReason_AndLeavesTheCaseNotVerified_CA_053_15_CA_053_32()
    {
        using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);
        var id = await SeedAsync(factory);

        using var withoutReason = await SendAsync(client, HttpMethod.Post, _route + "/" + id + "/decision", PanelReportKit.Token(verifier), new { decision = "rechazada" });
        var untouched = await Verifications(factory).Find(Builders<BsonDocument>.Filter.Eq("_id", id)).SingleAsync();
        using var withReason = await SendAsync(client, HttpMethod.Post, _route + "/" + id + "/decision", PanelReportKit.Token(verifier), new { decision = "rechazada", motivo = _reason });

        withoutReason.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        untouched["estado"].AsString.Should().Be("en_revision");
        untouched["llaveEnvuelta"].IsBsonNull.Should().BeFalse("una decisión rechazada por formato no toca la llave");
        withReason.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(withReason)).GetProperty("estado").GetString().Should().Be("no_verificada");
        var stored = await Verifications(factory).Find(Builders<BsonDocument>.Filter.Eq("_id", id)).SingleAsync();
        stored["curpVigente"].AsBoolean.Should().BeFalse();
        stored["llaveEnvuelta"].IsBsonNull.Should().BeTrue();
    }

    [Fact]
    public async Task DecidingAnUnknownOrAlreadyDecidedVerification_Answers404Or409AndChangesNothing_CA_053_17_CA_053_18()
    {
        using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);
        var decided = await SeedAsync(factory, state: "verificada");

        using var unknown = await SendAsync(client, HttpMethod.Post, _route + "/" + ObjectId.GenerateNewId() + "/decision", PanelReportKit.Token(verifier), new { decision = "aprobada" });
        using var conflict = await SendAsync(client, HttpMethod.Post, _route + "/" + decided + "/decision", PanelReportKit.Token(verifier), new { decision = "rechazada", motivo = _reason });

        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await JsonAsync(unknown)).GetProperty("code").GetString().Should().Be("verification_not_found");
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await JsonAsync(conflict)).GetProperty("code").GetString().Should().Be("verification_already_decided");
        (await Verifications(factory).Find(Builders<BsonDocument>.Filter.Eq("_id", decided)).SingleAsync())["estado"].AsString.Should().Be("verificada");
        (await Audit(factory).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("evento", "verificacion_decidida"))).Should().Be(0);
    }

    [Fact]
    public async Task ARepeatedDecision_LeavesASingleDecisionAndASingleAuditRecord_CA_053_40()
    {
        using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);
        var id = await SeedAsync(factory);

        using var first = await SendAsync(client, HttpMethod.Post, _route + "/" + id + "/decision", PanelReportKit.Token(verifier), new { decision = "aprobada" });
        using var repeated = await SendAsync(client, HttpMethod.Post, _route + "/" + id + "/decision", PanelReportKit.Token(verifier), new { decision = "aprobada" });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        repeated.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await AuditOfAsync(factory, id, "verificacion_decidida")).Should().ContainSingle();
    }

    [Fact]
    public async Task TenSimultaneousDecisions_LeaveExactlyOneDecisionAndOneAuditRecord_CA_053_40()
    {
        using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);
        var id = await SeedAsync(factory);
        var token = PanelReportKit.Token(verifier);

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => SendAsync(client, HttpMethod.Post, _route + "/" + id + "/decision", token, new { decision = "aprobada" })));

        try
        {
            // Un solo ganador y nueve 409 deterministas: nunca un 500 por interbloqueo de la base.
            responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
            responses.Where(r => r.StatusCode != HttpStatusCode.OK).Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Conflict);
            (await AuditOfAsync(factory, id, "verificacion_decidida")).Should().ContainSingle();
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task ABodyWithAnotherPersonsIdentifier_IsIgnored_AndTheDeciderIsTheTokenSub_CA_053_37()
    {
        using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);
        var impostor = Guid.NewGuid().ToString("D");
        var id = await SeedAsync(factory);

        using var response = await SendAsync(client, HttpMethod.Post, _route + "/" + id + "/decision", PanelReportKit.Token(verifier), new { decision = "aprobada", decididoPor = impostor, sub = impostor });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Verifications(factory).Find(Builders<BsonDocument>.Filter.Eq("_id", id)).SingleAsync())["decididoPor"].AsString.Should().Be(verifier);
        (await AuditOfAsync(factory, id, "verificacion_decidida"))[0]["sub"].AsString.Should().Be(verifier);
    }

    [Fact]
    public async Task TheVerifiedPerson_CannotOpenOrDecideTheirOwnVerification_AndNothingChanges_CA_053_08_CA_053_14()
    {
        using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);
        var id = await SeedAsync(factory, ownerSub: verifier);

        using var open = await SendAsync(client, HttpMethod.Get, _route + "/" + id, PanelReportKit.Token(verifier));
        using var decide = await SendAsync(client, HttpMethod.Post, _route + "/" + id + "/decision", PanelReportKit.Token(verifier), new { decision = "aprobada" });

        open.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        decide.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await JsonAsync(decide)).GetProperty("code").GetString().Should().Be("self_review_not_allowed");
        (await Verifications(factory).Find(Builders<BsonDocument>.Filter.Eq("_id", id)).SingleAsync())["estado"].AsString.Should().Be("en_revision");
    }
}
