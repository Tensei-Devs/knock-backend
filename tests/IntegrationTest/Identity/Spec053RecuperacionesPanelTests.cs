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
/// Spec 053 (T-04, D-07, CA-32 y CA-36) con la spec 039: las recuperaciones de acceso comparten la cola y la decisión de <c>/api/v1/admin/verificaciones</c>. Aprobar cambia el celular protegido de la cuenta,
/// cierra todas sus sesiones y audita en la misma transacción; rechazar solo cierra el caso; un celular ya registrado no cambia nada. MongoDB y Redis reales, solo datos ficticios; los celulares son bytes
/// al azar y se comparan por campo.
/// </summary>
public sealed class Spec053RecuperacionesPanelTests
{
    private const string _route = "/api/v1/admin/verificaciones";
    private const string _reason = "Datos de contacto confirmados";

    private static Task<IdentityApiFactory> HostAsync() => IdentityApiFactory.CreateAsync(PanelReportKit.HostSettings);

    private static IMongoCollection<BsonDocument> Recoveries(IdentityApiFactory factory) => factory.Database.GetCollection<BsonDocument>("recuperaciones");

    private static Task<List<BsonDocument>> AuditOfAsync(IdentityApiFactory factory, ObjectId id) =>
        factory.Database.GetCollection<BsonDocument>("panel_auditoria")
            .Find(Builders<BsonDocument>.Filter.Eq("recursoId", id.ToString()) & Builders<BsonDocument>.Filter.Eq("evento", "recuperacion_decidida")).ToListAsync();

    private static async Task<(ObjectId Id, byte[] NewHash)> SeedRecoveryAsync(IdentityApiFactory factory, ObjectId userId, string state = "pendiente", DateTime? createdAt = null, byte[]? newHash = null)
    {
        var id = ObjectId.GenerateNewId();
        var hash = newHash ?? RandomNumberGenerator.GetBytes(32);
        await Recoveries(factory).InsertOneAsync(new BsonDocument
        {
            { "_id", id },
            { "usuarioId", userId },
            { "estado", state },
            { "tipo", "tecnico" },
            {
                "celularNuevoProtegido", new BsonDocument
                {
                    { "cifrado", new BsonBinaryData(RandomNumberGenerator.GetBytes(48)) },
                    { "cifradoVersion", 1 },
                    { "hash", new BsonBinaryData(hash) },
                    { "hashVersion", 1 },
                }
            },
            { "creadoEn", new BsonDateTime(createdAt ?? DateTime.UtcNow.AddHours(-2)) },
            { "actualizadoEn", new BsonDateTime(DateTime.UtcNow.AddHours(-2)) },
        });
        return (id, hash);
    }

    private static async Task<ObjectId> SeedSessionAsync(IdentityApiFactory factory, ObjectId userId)
    {
        var id = ObjectId.GenerateNewId();
        await factory.Sessions.InsertOneAsync(new BsonDocument
        {
            { "_id", id }, { "usuarioId", userId }, { "dispositivo", "Teléfono de prueba" }, { "revocada", false },
            { "huellaRenovacion", new BsonBinaryData(RandomNumberGenerator.GetBytes(32)) }, { "huellasUsadas", new BsonArray() },
            { "creadaEn", new BsonDateTime(DateTime.UtcNow) }, { "venceEn", new BsonDateTime(DateTime.UtcNow.AddDays(30)) },
        });
        return id;
    }

    private static async Task<BsonDocument> UserAsync(IdentityApiFactory factory, ObjectId id) =>
        await factory.Users.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).SingleAsync();

    [Fact]
    public async Task TheQueueOfRecoveries_ListsOnlyPendingCasesOldestFirst_WithoutThePhone_CA_053_30_CA_053_32()
    {
        using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);
        var (user, _) = await PanelReportKit.SeedAccountAsync(factory.Users, true);
        var newer = await SeedRecoveryAsync(factory, user, createdAt: DateTime.UtcNow.AddHours(-1));
        var older = await SeedRecoveryAsync(factory, user, createdAt: DateTime.UtcNow.AddDays(-3));
        await SeedRecoveryAsync(factory, user, "aprobada");
        await SeedRecoveryAsync(factory, user, createdAt: DateTime.UtcNow.AddDays(-40));

        using var response = await SendAsync(client, HttpMethod.Get, _route + "?tipo=recuperacion", PanelReportKit.Token(verifier));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await JsonAsync(response)).GetProperty("items").EnumerateArray().ToList();
        items.Select(item => item.GetProperty("id").GetString()).Should().Equal(older.Id.ToString(), newer.Id.ToString());
        items.Should().OnlyContain(item => item.GetProperty("tipo").GetString() == "recuperacion" && item.GetProperty("estado").GetString() == "en_revision");
        items.SelectMany(item => item.EnumerateObject().Select(property => property.Name)).Distinct().Should().BeEquivalentTo("id", "tipo", "nivel", "estado", "enviadaEn");
    }

    [Fact]
    public async Task Approving_ChangesThePhone_ClosesAllSessions_AndAuditsInTheSameTransaction_CA_053_32_CA_053_36()
    {
        using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);
        var (user, _) = await PanelReportKit.SeedAccountAsync(factory.Users, true);
        var (otherUser, _) = await PanelReportKit.SeedAccountAsync(factory.Users, false);
        var first = await SeedSessionAsync(factory, user);
        var second = await SeedSessionAsync(factory, user);
        var foreign = await SeedSessionAsync(factory, otherUser);
        var recovery = await SeedRecoveryAsync(factory, user);

        using var response = await SendAsync(client, HttpMethod.Post, _route + "/" + recovery.Id + "/decision", PanelReportKit.Token(verifier), new { decision = "aprobada" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await JsonAsync(response);
        result.GetProperty("id").GetString().Should().Be(recovery.Id.ToString());
        result.GetProperty("estado").GetString().Should().Be("verificada");
        (await UserAsync(factory, user))["celularHash"].AsBsonBinaryData.Bytes.Should().Equal(recovery.NewHash);
        var sessions = await factory.Sessions.Find(Builders<BsonDocument>.Filter.In("_id", new[] { first, second, foreign })).ToListAsync();
        sessions.Where(s => s["usuarioId"].AsObjectId == user).Should().OnlyContain(s => s["revocada"].AsBoolean).And.HaveCount(2);
        sessions.Single(s => s["usuarioId"].AsObjectId == otherUser)["revocada"].AsBoolean.Should().BeFalse("las sesiones de otras cuentas no se tocan");
        (await factory.Redis.KeyExistsAsync("sesion-cancelada:" + first)).Should().BeTrue();
        var stored = await Recoveries(factory).Find(Builders<BsonDocument>.Filter.Eq("_id", recovery.Id)).SingleAsync();
        stored["estado"].AsString.Should().Be("aprobada");
        stored["decididoPor"].AsString.Should().Be(verifier);
        var entries = await AuditOfAsync(factory, recovery.Id);
        entries.Should().ContainSingle();
        entries[0]["sub"].AsString.Should().Be(verifier);
    }

    [Fact]
    public async Task Rejecting_ClosesTheCaseOnly_AndKeepsThePhoneAndTheSessions_CA_053_32()
    {
        using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);
        var (user, _) = await PanelReportKit.SeedAccountAsync(factory.Users, true);
        var before = (await UserAsync(factory, user))["celularHash"].AsBsonBinaryData.Bytes;
        var session = await SeedSessionAsync(factory, user);
        var recovery = await SeedRecoveryAsync(factory, user);

        using var response = await SendAsync(client, HttpMethod.Post, _route + "/" + recovery.Id + "/decision", PanelReportKit.Token(verifier), new { decision = "rechazada", motivo = _reason });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(response)).GetProperty("estado").GetString().Should().Be("no_verificada");
        (await UserAsync(factory, user))["celularHash"].AsBsonBinaryData.Bytes.Should().Equal(before);
        (await factory.Sessions.Find(Builders<BsonDocument>.Filter.Eq("_id", session)).SingleAsync())["revocada"].AsBoolean.Should().BeFalse();
        (await Recoveries(factory).Find(Builders<BsonDocument>.Filter.Eq("_id", recovery.Id)).SingleAsync())["estado"].AsString.Should().Be("rechazada");
        (await AuditOfAsync(factory, recovery.Id)).Should().ContainSingle();
    }

    [Fact]
    public async Task ANewPhoneThatAnotherAccountAlreadyHas_Answers409AndChangesNothing_CA_053_36()
    {
        using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);
        var (user, _) = await PanelReportKit.SeedAccountAsync(factory.Users, true);
        var (taker, _) = await PanelReportKit.SeedAccountAsync(factory.Users, false);
        var takenHash = (await UserAsync(factory, taker))["celularHash"].AsBsonBinaryData.Bytes;
        var before = (await UserAsync(factory, user))["celularHash"].AsBsonBinaryData.Bytes;
        var session = await SeedSessionAsync(factory, user);
        var recovery = await SeedRecoveryAsync(factory, user, newHash: takenHash);

        using var response = await SendAsync(client, HttpMethod.Post, _route + "/" + recovery.Id + "/decision", PanelReportKit.Token(verifier), new { decision = "aprobada" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await JsonAsync(response)).GetProperty("code").GetString().Should().Be("phone_already_registered");
        (await UserAsync(factory, user))["celularHash"].AsBsonBinaryData.Bytes.Should().Equal(before);
        (await factory.Sessions.Find(Builders<BsonDocument>.Filter.Eq("_id", session)).SingleAsync())["revocada"].AsBoolean.Should().BeFalse();
        (await Recoveries(factory).Find(Builders<BsonDocument>.Filter.Eq("_id", recovery.Id)).SingleAsync())["estado"].AsString.Should().Be("pendiente");
        (await AuditOfAsync(factory, recovery.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task ADecidedRecovery_Answers409_AndTenSimultaneousDecisionsLeaveOneDecisionAndOneAuditRecord_CA_053_18_CA_053_40()
    {
        using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);
        var (user, _) = await PanelReportKit.SeedAccountAsync(factory.Users, true);
        var recovery = await SeedRecoveryAsync(factory, user);
        var decided = await SeedRecoveryAsync(factory, user, "aprobada");
        var token = PanelReportKit.Token(verifier);

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => SendAsync(client, HttpMethod.Post, _route + "/" + recovery.Id + "/decision", token, new { decision = "aprobada" })));
        using var already = await SendAsync(client, HttpMethod.Post, _route + "/" + decided.Id + "/decision", token, new { decision = "rechazada", motivo = _reason });

        try
        {
            responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
            responses.Where(r => r.StatusCode != HttpStatusCode.OK).Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Conflict);
            (await AuditOfAsync(factory, recovery.Id)).Should().ContainSingle();
            already.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await JsonAsync(already)).GetProperty("code").GetString().Should().Be("verification_already_decided");
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
    public async Task AReviewerCannotDecideTheRecoveryOfTheirOwnAccount_AndNothingChanges_CA_053_14()
    {
        using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);
        var (user, _) = await PanelReportKit.SeedAccountAsync(factory.Users, true);
        await factory.Users.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", user), Builders<BsonDocument>.Update.Set("sub", verifier));
        var before = (await UserAsync(factory, user))["celularHash"].AsBsonBinaryData.Bytes;
        var session = await SeedSessionAsync(factory, user);
        var recovery = await SeedRecoveryAsync(factory, user);

        using var response = await SendAsync(client, HttpMethod.Post, _route + "/" + recovery.Id + "/decision", PanelReportKit.Token(verifier), new { decision = "aprobada" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await JsonAsync(response)).GetProperty("code").GetString().Should().Be("self_review_not_allowed");
        (await UserAsync(factory, user))["celularHash"].AsBsonBinaryData.Bytes.Should().Equal(before);
        (await factory.Sessions.Find(Builders<BsonDocument>.Filter.Eq("_id", session)).SingleAsync())["revocada"].AsBoolean.Should().BeFalse();
        (await Recoveries(factory).Find(Builders<BsonDocument>.Filter.Eq("_id", recovery.Id)).SingleAsync())["estado"].AsString.Should().Be("pendiente");
        (await AuditOfAsync(factory, recovery.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task WithoutTokenOrWithoutThePermission_TheDecisionIsRefused_AndNothingChanges_CA_053_36()
    {
        using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var moderator = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["moderador"]);
        var (user, _) = await PanelReportKit.SeedAccountAsync(factory.Users, true);
        var recovery = await SeedRecoveryAsync(factory, user);
        var path = _route + "/" + recovery.Id + "/decision";

        using var anonymous = await client.PostAsJsonAsync(new Uri(path, UriKind.Relative), new { decision = "aprobada" });
        using var denied = await SendAsync(client, HttpMethod.Post, path, PanelReportKit.Token(moderator), new { decision = "aprobada" });

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Recoveries(factory).Find(Builders<BsonDocument>.Filter.Eq("_id", recovery.Id)).SingleAsync())["estado"].AsString.Should().Be("pendiente");
        (await AuditOfAsync(factory, recovery.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task RepeatingAnApprovalThatJustConfirmed_CancelsTheClosedSessionsAgain_AndAnswers409_CA_053_36()
    {
        using var factory = await HostAsync();
        using var client = factory.CreateClient();
        var verifier = await PanelReportKit.SeedPanelAccountAsync(factory.Services, ["verificador"]);
        var (user, _) = await PanelReportKit.SeedAccountAsync(factory.Users, true);
        var session = await SeedSessionAsync(factory, user);
        var recovery = await SeedRecoveryAsync(factory, user, "aprobada");
        // Como si Redis hubiera fallado tras confirmar: la sesión está revocada en la base y no consta en la lista del gateway.
        await factory.Sessions.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", session), Builders<BsonDocument>.Update.Set("revocada", true));
        await Recoveries(factory).UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", recovery.Id), Builders<BsonDocument>.Update.Set("decididoEn", new BsonDateTime(DateTime.UtcNow)));
        (await factory.Redis.KeyExistsAsync("sesion-cancelada:" + session)).Should().BeFalse();

        using var response = await SendAsync(client, HttpMethod.Post, _route + "/" + recovery.Id + "/decision", PanelReportKit.Token(verifier), new { decision = "aprobada" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await factory.Redis.KeyExistsAsync("sesion-cancelada:" + session)).Should().BeTrue();
    }
}
