extern alias IdentityApi;

using System.Security.Cryptography;
using AwesomeAssertions;
using IdentityApi::Knock.Identity.Api.Modules.Safety.Services;
using IdentityApi::Knock.Identity.Api.Modules.Sesiones.Data;
using Knock.IntegrationTest.Support;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Identity;

/// <summary>
/// Spec 059, T-06: la suspensión preventiva y la reactivación con MongoDB y Redis reales. La decisión del panel (spec 053) se ejercita en pruebas unitarias; aquí se prueba la transacción
/// de <see cref="AccountSuspension"/>, sus eventos de bandeja, la auditoría y la cancelación de sesiones. Solo datos ficticios; los valores al azar se comparan por campo.
/// </summary>
public sealed class Spec059SuspensionTests
{
    private static readonly string _moderator = Guid.NewGuid().ToString("D");

    private static async Task<(ObjectId Id, string Sub)> SeedAccountAsync(IdentityApiFactory factory, bool technician, string state = "activa")
    {
        var id = ObjectId.GenerateNewId();
        var sub = Guid.NewGuid().ToString("D");
        await factory.Users.InsertOneAsync(new BsonDocument
        {
            { "_id", id }, { "sub", sub }, { "nombre", "Demo Reportado" }, { "esTecnico", technician }, { "estado", state }, { "perfilCompleto", true },
            { "creadoEn", new BsonDateTime(DateTime.UtcNow) }, { "version", 1 }, { "celularHash", new BsonBinaryData(RandomNumberGenerator.GetBytes(32)) },
        });
        return (id, sub);
    }

    private static async Task<List<ObjectId>> SeedSessionsAsync(IdentityApiFactory factory, ObjectId user, int count)
    {
        var ids = new List<ObjectId>();
        for (var i = 0; i < count; i++)
        {
            var id = ObjectId.GenerateNewId();
            ids.Add(id);
            await factory.Sessions.InsertOneAsync(new BsonDocument { { "_id", id }, { "usuarioId", user }, { "revocada", false }, { "huellaRenovacion", Convert.ToHexString(RandomNumberGenerator.GetBytes(16)) }, { "venceEn", new BsonDateTime(DateTime.UtcNow.AddDays(30)) } });
        }

        return ids;
    }

    private static Task<List<BsonDocument>> OutboxAsync(IdentityApiFactory factory, string type) =>
        factory.Database.GetCollection<BsonDocument>("outbox").Find(Builders<BsonDocument>.Filter.Eq("tipo", type)).ToListAsync();

    private static Task<List<BsonDocument>> AuditAsync(IdentityApiFactory factory, string reportId) =>
        factory.Database.GetCollection<BsonDocument>("panel_auditoria").Find(Builders<BsonDocument>.Filter.Eq("recursoId", reportId)).ToListAsync();

    [Fact]
    public async Task Suspending_A_Technician_Revokes_Sessions_Leaves_Events_And_Audit_And_Cancels_In_Redis_CA_059_01()
    {
        await using var factory = await IdentityApiFactory.CreateAsync();
        var (id, sub) = await SeedAccountAsync(factory, technician: true);
        var sessions = await SeedSessionsAsync(factory, id, 2);
        var reportId = ObjectId.GenerateNewId().ToString();
        var service = factory.Services.GetRequiredService<AccountSuspension>();

        var result = await service.SuspenderAsync(sub, reportId, _moderator, "preventiva", CancellationToken.None);

        result.Outcome.Should().Be(SuspensionOutcome.Suspended);
        var user = await factory.Users.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync();
        user["estado"].AsString.Should().Be("suspendida");
        user["suspension"]["tipo"].AsString.Should().Be("preventiva");
        user["suspension"]["reporteId"].AsObjectId.ToString().Should().Be(reportId);
        (await factory.Sessions.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("usuarioId", id) & Builders<BsonDocument>.Filter.Eq("revocada", true))).Should().Be(2);
        var suspended = await OutboxAsync(factory, "CuentaSuspendida");
        suspended.Should().ContainSingle();
        suspended[0]["destino"].AsString.Should().Be("market");
        suspended[0]["carga"]["usuarioId"].AsString.Should().Be(sub);
        suspended[0]["carga"].AsBsonDocument.Names.Should().BeEquivalentTo("eventoId", "usuarioId", "reporteId", "desde");
        (await OutboxAsync(factory, "sesion_revocada")).Select(e => e["carga"]["sid"].AsString).Should().BeEquivalentTo(sessions.Select(s => s.ToString()));
        var audit = await AuditAsync(factory, reportId);
        audit.Should().ContainSingle(entry => entry["evento"].AsString == "cuenta_suspendida");
        foreach (var sid in sessions)
        {
            (await factory.Redis.KeyExistsAsync(RedisSesionCancelada.Key(sid.ToString()))).Should().BeTrue();
        }
    }

    [Fact]
    public async Task A_Client_Is_Not_Suspended_And_Keeps_Its_Sessions_CA_059_04()
    {
        await using var factory = await IdentityApiFactory.CreateAsync();
        var (id, sub) = await SeedAccountAsync(factory, technician: false);
        await SeedSessionsAsync(factory, id, 1);

        var result = await factory.Services.GetRequiredService<AccountSuspension>().SuspenderAsync(sub, ObjectId.GenerateNewId().ToString(), _moderator, "preventiva", CancellationToken.None);

        result.Outcome.Should().Be(SuspensionOutcome.NotTechnician);
        (await factory.Users.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync())["estado"].AsString.Should().Be("activa");
        (await factory.Sessions.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("revocada", true))).Should().Be(0);
        (await OutboxAsync(factory, "CuentaSuspendida")).Should().BeEmpty();
    }

    [Fact]
    public async Task Repeated_And_Simultaneous_Suspensions_Produce_One_Change_And_One_Event_CA_059_06()
    {
        await using var factory = await IdentityApiFactory.CreateAsync();
        var (id, sub) = await SeedAccountAsync(factory, technician: true);
        await SeedSessionsAsync(factory, id, 1);
        var reportId = ObjectId.GenerateNewId().ToString();
        var service = factory.Services.GetRequiredService<AccountSuspension>();

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Attempt(service, sub, reportId)));
        var again = await service.SuspenderAsync(sub, reportId, _moderator, "preventiva", CancellationToken.None);

        results.Count(outcome => outcome == SuspensionOutcome.Suspended).Should().Be(1);
        again.Outcome.Should().Be(SuspensionOutcome.AlreadySuspended);
        (await OutboxAsync(factory, "CuentaSuspendida")).Should().ContainSingle();
        (await OutboxAsync(factory, "sesion_revocada")).Should().ContainSingle();
        (await AuditAsync(factory, reportId)).Count(entry => entry["evento"].AsString == "cuenta_suspendida").Should().Be(1);
    }

    private static async Task<SuspensionOutcome> Attempt(AccountSuspension service, string sub, string reportId)
    {
        try
        {
            return (await service.SuspenderAsync(sub, reportId, _moderator, "preventiva", CancellationToken.None)).Outcome;
        }
        catch (MongoException)
        {
            return SuspensionOutcome.AlreadySuspended;
        }
    }

    [Fact]
    public async Task A_Moderator_Cannot_Suspend_Their_Own_Account_And_Nothing_Changes_CA_059_11()
    {
        await using var factory = await IdentityApiFactory.CreateAsync();
        var (id, sub) = await SeedAccountAsync(factory, technician: true);
        var service = factory.Services.GetRequiredService<AccountSuspension>();

        var act = () => service.SuspenderAsync(sub, ObjectId.GenerateNewId().ToString(), sub, "preventiva", CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
        (await factory.Users.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync())["estado"].AsString.Should().Be("activa");
        (await OutboxAsync(factory, "CuentaSuspendida")).Should().BeEmpty();
    }

    [Fact]
    public async Task Reactivating_Restores_The_Account_Emits_The_Event_Audits_And_Does_Not_Restore_Sessions_CA_059_14()
    {
        await using var factory = await IdentityApiFactory.CreateAsync();
        var (id, sub) = await SeedAccountAsync(factory, technician: true);
        await SeedSessionsAsync(factory, id, 1);
        var reportId = ObjectId.GenerateNewId().ToString();
        var service = factory.Services.GetRequiredService<AccountSuspension>();
        await service.SuspenderAsync(sub, reportId, _moderator, "preventiva", CancellationToken.None);

        await service.ReactivarAsync(sub, reportId, _moderator, CancellationToken.None);

        var user = await factory.Users.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync();
        user["estado"].AsString.Should().Be("activa");
        user.Contains("suspension").Should().BeFalse();
        (await OutboxAsync(factory, "CuentaReactivada")).Should().ContainSingle();
        (await AuditAsync(factory, reportId)).Should().Contain(entry => entry["evento"].AsString == "cuenta_reactivada");
        (await factory.Sessions.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("usuarioId", id) & Builders<BsonDocument>.Filter.Eq("revocada", false))).Should().Be(0);
    }

    [Fact]
    public async Task Reactivating_An_Account_That_Is_Not_Suspended_Is_Rejected_Without_Changes_CA_059_15()
    {
        await using var factory = await IdentityApiFactory.CreateAsync();
        var (_, sub) = await SeedAccountAsync(factory, technician: true);
        var service = factory.Services.GetRequiredService<AccountSuspension>();

        var act = () => service.ReactivarAsync(sub, ObjectId.GenerateNewId().ToString(), _moderator, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
        (await OutboxAsync(factory, "CuentaReactivada")).Should().BeEmpty();
    }
}
