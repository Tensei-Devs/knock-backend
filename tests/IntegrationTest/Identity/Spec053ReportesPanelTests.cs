extern alias IdentityApi;

using AwesomeAssertions;
using IdentityApi::Knock.Identity.Api.Common.Errors;
using IdentityApi::Knock.Identity.Api.Modules.Moderation.Data;
using IdentityApi::Knock.Identity.Api.Modules.Moderation.Dtos;
using IdentityApi::Knock.Identity.Api.Modules.Moderation.Services;
using Knock.IntegrationTest.Support;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Identity;

/// <summary>
/// Spec 053, REP-04 y REP-05 (T-05 y T-06) sobre MongoDB real: la cola por gravedad y antigüedad, y la decisión con sus efectos y su auditoría en una sola transacción. Se prueba el servicio
/// y el almacén del módulo (el panel exige doble factor, que cubre la spec 033). Solo datos ficticios.
/// </summary>
public sealed class Spec053ReportesPanelTests
{
    private const string _reporter = "11111111-1111-4111-8111-111111111111";
    private const string _reported = "22222222-2222-4222-8222-222222222222";
    private const string _moderator = "33333333-3333-4333-8333-333333333333";
    private const string _secretText = "Texto privado del reporte";

    private static async Task<ObjectId> SeedReportAsync(IdentityApiFactory factory, string? severity = null, DateTime? createdAt = null, string state = "abierto", string? reported = null)
    {
        var id = ObjectId.GenerateNewId();
        var document = new BsonDocument
        {
            { "_id", id }, { "reportaId", _reporter }, { "reportadoId", reported ?? _reported }, { "motivo", "no_llego" }, { "descripcion", _secretText },
            { "estado", state }, { "creadoEn", new BsonDateTime(createdAt ?? DateTime.UtcNow) },
        };
        if (severity is not null)
        {
            document["gravedad"] = severity;
        }

        await factory.Database.GetCollection<BsonDocument>("reportes").InsertOneAsync(document);
        return id;
    }

    private static ReportDecisionBody Decision(string severity) => new() { Gravedad = severity, Motivo = "Revisado por el equipo de prueba" };

    private static ReviewActor Moderator() => new(_moderator, "sid-prueba");

    private static Task<BsonDocument> ReadReportAsync(IdentityApiFactory factory, ObjectId id) =>
        factory.Database.GetCollection<BsonDocument>("reportes").Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync();

    private static Task<long> CountOutboxAsync(IdentityApiFactory factory) =>
        factory.Database.GetCollection<BsonDocument>("outbox").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

    [Fact]
    public async Task TheQueue_OrdersBySeverityThenAge_AndHidesDecidedReports_CA_053_19()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        var now = DateTime.UtcNow;
        var lowOld = await SeedReportAsync(factory, "baja", now.AddHours(-5));
        var criticalNew = await SeedReportAsync(factory, "critica", now.AddHours(-1));
        var unrated = await SeedReportAsync(factory, null, now.AddHours(-9));
        var criticalOld = await SeedReportAsync(factory, "critica", now.AddHours(-3));
        await SeedReportAsync(factory, "alta", now.AddHours(-2), "decidido");
        var service = factory.Services.GetRequiredService<ReportReviewService>();

        var page = await service.ListAsync(new ReportQueueQuery(), CancellationToken.None);

        page.Items.Select(item => item.Id).Should().Equal(criticalOld.ToString(), criticalNew.ToString(), lowOld.ToString(), unrated.ToString());
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task TheQueue_PagesByCursor_WithoutRepeatingOrSkipping_CA_053_21()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        var ids = new List<string>();
        for (var index = 0; index < 5; index++)
        {
            ids.Add((await SeedReportAsync(factory, "media", DateTime.UtcNow.AddMinutes(-10 + index))).ToString());
        }

        var service = factory.Services.GetRequiredService<ReportReviewService>();
        var first = await service.ListAsync(new ReportQueueQuery { Limite = 3 }, CancellationToken.None);
        var second = await service.ListAsync(new ReportQueueQuery { Limite = 3, Cursor = first.NextCursor }, CancellationToken.None);

        first.Items.Should().HaveCount(3);
        first.NextCursor.Should().NotBeNull();
        second.Items.Should().HaveCount(2);
        second.NextCursor.Should().BeNull();
        first.Items.Concat(second.Items).Select(item => item.Id).Should().Equal(ids);
    }

    [Fact]
    public async Task DecidingHigh_SavesTheDecision_HidesTheTechnician_NotifiesBothParties_AndAudits_CA_053_26()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        var id = await SeedReportAsync(factory);
        var service = factory.Services.GetRequiredService<ReportReviewService>();

        var result = await service.DecideAsync(Moderator(), id.ToString(), Decision("alta"), CancellationToken.None);

        result.Estado.Should().Be("decidido");
        result.Gravedad.Should().Be("alta");
        var report = await ReadReportAsync(factory, id);
        report["estado"].AsString.Should().Be("decidido");
        report["decision"]["decididoPor"].AsString.Should().Be(_moderator);
        var events = await factory.Database.GetCollection<BsonDocument>("outbox").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        events.Select(e => e["tipo"].AsString).Should().BeEquivalentTo("TecnicoOcultado", "ReporteDecidido", "ReporteDecidido");
        events.Single(e => e["tipo"].AsString == "TecnicoOcultado")["destino"].AsString.Should().Be("market");
        events.Select(e => e.ToJson()).Should().OnlyContain(json => !json.Contains(_secretText));
        (await factory.Database.GetCollection<BsonDocument>("panel_auditoria").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EachPartyGetsOnlyItsRole_NeverTheOtherPartyId_CA_053_28()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        var id = await SeedReportAsync(factory);

        await factory.Services.GetRequiredService<ReportReviewService>().DecideAsync(Moderator(), id.ToString(), Decision("media"), CancellationToken.None);

        var outbox = factory.Database.GetCollection<BsonDocument>("outbox");
        var notices = await outbox.Find(Builders<BsonDocument>.Filter.Eq("tipo", "ReporteDecidido")).ToListAsync();
        notices.Should().HaveCount(2);
        var toReporter = notices.Single(n => n["carga"]["rol"].AsString == "reportante");
        var toReported = notices.Single(n => n["carga"]["rol"].AsString == "reportado");
        toReporter["carga"]["usuarioId"].AsString.Should().Be(_reporter);
        toReporter.ToJson().Should().NotContain(_reported);
        toReported["carga"]["usuarioId"].AsString.Should().Be(_reported);
        toReported.ToJson().Should().NotContain(_reporter);
        (await outbox.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("tipo", "TecnicoOcultado"))).Should().Be(0);
    }

    [Fact]
    public async Task DecidingTwice_Answers409_AndWritesNoSecondSetOfEvents_CA_053_27()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        var id = await SeedReportAsync(factory);
        var service = factory.Services.GetRequiredService<ReportReviewService>();
        await service.DecideAsync(Moderator(), id.ToString(), Decision("alta"), CancellationToken.None);
        var before = await CountOutboxAsync(factory);

        var second = () => service.DecideAsync(Moderator(), id.ToString(), Decision("baja"), CancellationToken.None);

        (await second.Should().ThrowAsync<DomainException>()).Which.Kind.Should().Be(DomainErrorKind.Conflict);
        (await CountOutboxAsync(factory)).Should().Be(before);
        (await ReadReportAsync(factory, id))["decision"]["gravedad"].AsString.Should().Be("alta");
    }

    [Fact]
    public async Task TenSimultaneousDecisions_LeaveExactlyOneDecision_AndOneSetOfEvents_CA_053_27()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        var id = await SeedReportAsync(factory);
        var service = factory.Services.GetRequiredService<ReportReviewService>();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            try
            {
                await service.DecideAsync(Moderator(), id.ToString(), Decision("alta"), CancellationToken.None);
                return true;
            }
            catch (Exception exception) when (exception is DomainException or MongoCommandException)
            {
                // El perdedor recibe 409 o, bajo contención en documentdb-local, un tiempo de bloqueo agotado; ninguno deja efectos.
                return false;
            }
        }));

        outcomes.Count(decided => decided).Should().Be(1);
        (await CountOutboxAsync(factory)).Should().Be(3);
    }

    [Fact]
    public async Task AModeratorInTheReport_CannotDecideIt_AndNothingChanges_CA_053_29()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        var id = await SeedReportAsync(factory, reported: _moderator);

        var attempt = () => factory.Services.GetRequiredService<ReportReviewService>().DecideAsync(Moderator(), id.ToString(), Decision("alta"), CancellationToken.None);

        (await attempt.Should().ThrowAsync<DomainException>()).Which.Kind.Should().Be(DomainErrorKind.Forbidden);
        (await ReadReportAsync(factory, id))["estado"].AsString.Should().Be("abierto");
        (await CountOutboxAsync(factory)).Should().Be(0);
    }

    [Fact]
    public async Task AnUnknownReport_Answers404_CA_053_24()
    {
        using var factory = await IdentityApiFactory.CreateAsync();

        var attempt = () => factory.Services.GetRequiredService<ReportReviewService>().DecideAsync(Moderator(), ObjectId.GenerateNewId().ToString(), Decision("alta"), CancellationToken.None);

        (await attempt.Should().ThrowAsync<DomainException>()).Which.Kind.Should().Be(DomainErrorKind.NotFound);
    }

    [Fact]
    public async Task IfTheAuditFails_NeitherTheDecisionNorTheEventsRemain_CA_053_32()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        var id = await SeedReportAsync(factory);
        var store = factory.Services.GetRequiredService<IReportReviewStore>();
        var record = new ReportDecisionRecord(id, "alta", "ocultar_tecnico", "Motivo de prueba", _moderator, DateTime.UtcNow);

        var attempt = () => store.DecideAsync(record, (_, _) => Task.CompletedTask, _ => throw new InvalidOperationException("audit down"), CancellationToken.None);

        await attempt.Should().ThrowAsync<InvalidOperationException>();
        var report = await ReadReportAsync(factory, id);
        report["estado"].AsString.Should().Be("abierto");
        report.Contains("decision").Should().BeFalse();
    }
}
