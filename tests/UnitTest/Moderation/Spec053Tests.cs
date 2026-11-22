using AwesomeAssertions;
using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Moderation.Data;
using Knock.Identity.Api.Modules.Moderation.Domain;
using Knock.Identity.Api.Modules.Moderation.Dtos;
using Knock.Identity.Api.Modules.Moderation.Services;
using Knock.Identity.Api.Modules.Moderation.Validators;
using Knock.Identity.Api.Modules.Panel;
using Knock.Identity.Api.Modules.Panel.Data;
using Knock.Identity.Api.Modules.Panel.Services;
using Knock.Identity.Api.Modules.Trust;
using Knock.UnitTest.Identity;
using Knock.UnitTest.Panel;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Moderation;

/// <summary>Spec 053 (KNOCK-86): reglas de dominio, validadores y servicios de las colas y decisiones del panel, con dobles en memoria (sin MongoDB).</summary>
public class Spec053Tests
{
    private const string _owner = "owner-sub";
    private static readonly string _id = ObjectId.GenerateNewId().ToString();
    private static readonly ReviewActor _verifier = new("verifier-sub", "sid-1");

    private static StoreTransaction Session() => new(new object());

    private sealed class FakeAudit : IPanelAuditoriaStore, IPanelAuditoriaEnTransaccion
    {
        public List<PanelAuditEntry> Entries { get; } = [];

        public bool Fails { get; set; }

        public Task InsertAsync(PanelAuditEntry entry, CancellationToken cancellationToken) => Add(entry);

        public Task InsertAsync(PanelAuditEntry entry, StoreTransaction transaction, CancellationToken cancellationToken) => Add(entry);

        private Task Add(PanelAuditEntry entry)
        {
            if (Fails)
            {
                throw new TimeoutException("conexion caida");
            }

            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeVerifications : IVerificationReviewStore
    {
        public ReviewedVerification? Stored { get; set; }

        public DecisionStoreOutcome Outcome { get; set; } = DecisionStoreOutcome.Decided;

        public VerificationDecisionRecord? Decided { get; private set; }

        public Task<IReadOnlyList<QueuedVerification>> ListAsync(string state, DateTime from, DateTime to, ReviewCursor? after, int take, CancellationToken cancellationToken)
        {
            IReadOnlyList<QueuedVerification> rows = [.. Enumerable.Range(0, take).Select(i => new QueuedVerification(ObjectId.GenerateNewId(), "basica", state, from.AddMinutes(i)))];
            return Task.FromResult(rows);
        }

        public Task<ReviewedVerification?> FindAsync(ObjectId id, CancellationToken cancellationToken) => Task.FromResult(Stored);

        public async Task<DecisionStoreOutcome> DecideAsync(VerificationDecisionRecord decision, Func<StoreTransaction, Task> writeAudit, CancellationToken cancellationToken)
        {
            if (Outcome == DecisionStoreOutcome.Decided)
            {
                Decided = decision;
                await writeAudit(Session());
            }

            return Outcome;
        }
    }

    private sealed class FakeLinks : IVerificationPhotoLinks
    {
        public int Issued { get; private set; }

        public TimeSpan? Lifetime { get; private set; }

        public Task<IReadOnlyList<VerificationPhotoLink>> IssueAsync(string verificationId, IReadOnlyList<string> photoTypes, TimeSpan lifetime, CancellationToken cancellationToken)
        {
            Issued++;
            Lifetime = lifetime;
            IReadOnlyList<VerificationPhotoLink> links = [.. photoTypes.Select(t => new VerificationPhotoLink(t, "https://blob.example/x", DateTime.UtcNow.Add(lifetime)))];
            return Task.FromResult(links);
        }
    }

    private sealed class FakeBlobs : IVerificationBlobPurger
    {
        public bool Fails { get; set; }

        public int Calls { get; private set; }

        public Task PurgeBlobsAsync(string verificationId, CancellationToken cancellationToken)
        {
            Calls++;
            return Fails ? throw new InvalidOperationException("blob no disponible") : Task.CompletedTask;
        }
    }

    private sealed class FakeReports : IReportReviewStore
    {
        public DecidedReportParties? Parties { get; set; } = new("reporter", "reported", "acoso");

        public DecisionStoreOutcome Outcome { get; set; } = DecisionStoreOutcome.Decided;

        public ReportDecisionRecord? Decided { get; private set; }

        public Task<IReadOnlyList<QueuedReport>> ListAsync(string? severity, IReadOnlyList<string> states, DateTime from, DateTime to, ReviewCursor? after, int take, CancellationToken cancellationToken)
        {
            IReadOnlyList<QueuedReport> rows = [.. Enumerable.Range(0, take).Select(i => new QueuedReport(ObjectId.GenerateNewId(), "acoso", "texto del reporte", severity, states[0], from.AddMinutes(i), i))];
            return Task.FromResult(rows);
        }

        public Task<DecidedReportParties?> FindPartiesAsync(ObjectId id, CancellationToken cancellationToken) => Task.FromResult(Parties);

        public async Task<ReportDecisionStoreResult> DecideAsync(ReportDecisionRecord decision, Func<StoreTransaction, DecidedReportParties, Task> applyEffects, Func<StoreTransaction, Task> writeAudit, CancellationToken cancellationToken)
        {
            if (Outcome != DecisionStoreOutcome.Decided)
            {
                return new ReportDecisionStoreResult(Outcome);
            }

            Decided = decision;
            await applyEffects(Session(), Parties!);
            await writeAudit(Session());
            return new ReportDecisionStoreResult(Outcome, Parties);
        }
    }

    private sealed class RecordingEffects : IReportDecisionEffects
    {
        public ReportDecisionApplied? Applied { get; private set; }

        public Task ApplyAsync(StoreTransaction transaction, ReportDecisionApplied decision, CancellationToken cancellationToken)
        {
            Applied = decision;
            return Task.CompletedTask;
        }
    }

    private static (VerificationReviewService Service, FakeVerifications Store, FakeLinks Links, FakeBlobs Blobs, FakeAudit Audit) NewVerificationService(bool underReview = true)
    {
        var store = new FakeVerifications
        {
            Stored = new ReviewedVerification(ObjectId.Parse(_id), _owner, "basica", underReview ? "en_revision" : "verificada", new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc), ["ine_frente", "selfie"], false),
        };
        var links = new FakeLinks();
        var blobs = new FakeBlobs();
        var audit = new FakeAudit();
        var world = new PanelClock(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero));
        var auditService = new AuditoriaPanel(audit, new PanelIpHasher(PhoneProtectionTestKeys.Keys()), new NoHttpContext(), world, NullLogger<AuditoriaPanel>.Instance);
        var service = new VerificationReviewService(store, new NoAccessRecoveryReviews(), links, blobs, new PanelApi(auditService), new TrustApi(null!), world, NullLogger<VerificationReviewService>.Instance);
        return (service, store, links, blobs, audit);
    }

    [Fact]
    public async Task Abrir_Entrega_Enlaces_De_5_Minutos_Y_Registra_Quien_Abre_CA_053_31()
    {
        var (service, _, links, _, audit) = NewVerificationService();

        var detail = await service.OpenAsync(_verifier, _id, CancellationToken.None);

        detail.Fotos.Should().HaveCount(2);
        links.Lifetime.Should().Be(TimeSpan.FromMinutes(5));
        audit.Entries.Should().ContainSingle().Which.Should().Match<PanelAuditEntry>(e => e.Evento == "verificacion_abierta" && e.Sub == "verifier-sub" && e.RecursoId == _id);
    }

    [Fact]
    public async Task Si_No_Se_Puede_Auditar_La_Apertura_No_Se_Entregan_Enlaces_CA_053_36()
    {
        var (service, _, links, _, audit) = NewVerificationService();
        audit.Fails = true;

        var act = () => service.OpenAsync(_verifier, _id, CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("audit_unavailable");
        links.Issued.Should().Be(0);
    }

    [Fact]
    public async Task Fotos_Destruidas_Responde_410_Photos_Destroyed_CA_053_11()
    {
        var (service, store, _, _, _) = NewVerificationService();
        store.Stored = store.Stored! with { PhotosDestroyed = true };

        var act = () => service.OpenAsync(_verifier, _id, CancellationToken.None);

        var error = (await act.Should().ThrowAsync<DomainException>()).Which;
        error.Kind.Should().Be(DomainErrorKind.Gone);
        error.Code.Should().Be("photos_destroyed");
        DomainErrors.ToStatusCode(error.Kind).Should().Be(410);
    }

    [Fact]
    public async Task Verificacion_Inexistente_Responde_404_CA_053_10()
    {
        var (service, store, _, _, _) = NewVerificationService();
        store.Stored = null;

        var act = () => service.OpenAsync(_verifier, _id, CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("verification_not_found");
    }

    [Fact]
    public async Task La_Persona_Verificada_No_Puede_Abrir_Ni_Decidir_Su_Propia_Verificacion_D_05()
    {
        var (service, _, links, _, _) = NewVerificationService();
        var self = new ReviewActor(_owner, "sid");

        var open = () => service.OpenAsync(self, _id, CancellationToken.None);
        var decide = () => service.DecideAsync(self, _id, new VerificationDecisionBody { Decision = "aprobada" }, CancellationToken.None);

        (await open.Should().ThrowAsync<DomainException>()).Which.Kind.Should().Be(DomainErrorKind.Forbidden);
        (await decide.Should().ThrowAsync<DomainException>()).Which.Kind.Should().Be(DomainErrorKind.Forbidden);
        links.Issued.Should().Be(0);
    }

    [Fact]
    public async Task Aprobar_Guarda_La_Decision_Audita_Con_El_Sub_Del_Token_Y_Borra_Blobs_CA_053_32()
    {
        var (service, store, _, blobs, audit) = NewVerificationService();

        var result = await service.DecideAsync(_verifier, _id, new VerificationDecisionBody { Decision = "aprobada" }, CancellationToken.None);

        result.Estado.Should().Be("verificada");
        store.Decided!.Approved.Should().BeTrue();
        store.Decided.DeciderSub.Should().Be("verifier-sub");
        audit.Entries.Should().ContainSingle().Which.Should().Match<PanelAuditEntry>(e => e.Evento == "verificacion_decidida" && e.Resultado == "aprobada" && e.Sub == "verifier-sub");
        blobs.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Rechazar_Conserva_El_Motivo_Del_Catalogo_Y_No_Lo_Escribe_En_La_Auditoria_CA_053_32()
    {
        var (service, store, _, _, audit) = NewVerificationService();

        await service.DecideAsync(_verifier, _id, new VerificationDecisionBody { Decision = "rechazada", Motivo = "foto_ilegible" }, CancellationToken.None);

        store.Decided!.CatalogReason.Should().Be("foto_ilegible");
        audit.Entries.Single().Should().Match<PanelAuditEntry>(e => e.Resultado == "rechazada" && e.Permiso == null && e.Ruta == null);
    }

    [Fact]
    public async Task Decidir_Una_Ya_Decidida_Responde_409_CA_053_18()
    {
        var (service, store, _, _, _) = NewVerificationService();
        store.Outcome = DecisionStoreOutcome.AlreadyDecided;

        var act = () => service.DecideAsync(_verifier, _id, new VerificationDecisionBody { Decision = "aprobada" }, CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("verification_already_decided");
    }

    [Fact]
    public async Task Un_Curp_Duplicado_Al_Aprobar_Responde_409_Sin_Cambiar_Nada_Spec025_R_06()
    {
        var (service, store, _, blobs, _) = NewVerificationService();
        store.Outcome = DecisionStoreOutcome.CurpDuplicate;

        var act = () => service.DecideAsync(_verifier, _id, new VerificationDecisionBody { Decision = "aprobada" }, CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("curp_duplicate");
        blobs.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Un_Fallo_Al_Borrar_Blobs_No_Revierte_La_Decision_Spec057_TC_HU_068_10()
    {
        var (service, _, _, blobs, _) = NewVerificationService();
        blobs.Fails = true;

        var result = await service.DecideAsync(_verifier, _id, new VerificationDecisionBody { Decision = "aprobada" }, CancellationToken.None);

        result.Estado.Should().Be("verificada");
    }

    [Fact]
    public async Task Si_La_Auditoria_Falla_La_Decision_Responde_503_CA_053_36()
    {
        var (service, _, _, _, audit) = NewVerificationService();
        audit.Fails = true;

        var act = () => service.DecideAsync(_verifier, _id, new VerificationDecisionBody { Decision = "aprobada" }, CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("audit_unavailable");
    }

    [Fact]
    public async Task Una_Decision_Sobre_Un_Id_Que_No_Es_Verificacion_Ni_Recuperacion_Responde_404_CA_053_17()
    {
        var (service, store, _, _, _) = NewVerificationService();
        store.Stored = null;

        var act = () => service.DecideAsync(_verifier, _id, new VerificationDecisionBody { Decision = "aprobada" }, CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("verification_not_found");
    }

    [Fact]
    public async Task La_Cola_Pagina_Por_Cursor_Y_Pide_Una_Fila_De_Mas_CA_053_05()
    {
        var (service, _, _, _, _) = NewVerificationService();

        var page = await service.ListAsync(new VerificationQueueQuery { Limite = 20 }, CancellationToken.None);

        page.Items.Should().HaveCount(20);
        page.NextCursor.Should().NotBeNullOrEmpty();
        ReviewCursor.Parse(page.NextCursor).Should().NotBeNull();
        page.Items.Should().OnlyContain(i => i.Tipo == "identidad" && i.Estado == "en_revision");
    }

    [Theory]
    [InlineData("critica", "suspension_preventiva")]
    [InlineData("alta", "ocultar")]
    [InlineData("media", "advertencia")]
    [InlineData("baja", "mediacion")]
    public async Task La_Respuesta_Se_Deriva_De_La_Gravedad_Y_Llega_A_Los_Efectos_CA_053_34(string severity, string response)
    {
        var store = new FakeReports();
        var effects = new RecordingEffects();
        var audit = new FakeAudit();
        var clock = new PanelClock(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero));
        var service = new ReportReviewService(store, effects, new PanelApi(new AuditoriaPanel(audit, new PanelIpHasher(PhoneProtectionTestKeys.Keys()), new NoHttpContext(), clock, NullLogger<AuditoriaPanel>.Instance)), clock);

        var result = await service.DecideAsync(_verifier, _id, new ReportDecisionBody { Gravedad = severity, Motivo = "motivo suficiente" }, CancellationToken.None);

        result.Respuesta.Should().Be(response);
        effects.Applied.Should().Be(new ReportDecisionApplied(_id, severity, response, "reporter", "reported", "verifier-sub"));
        audit.Entries.Single().Should().Match<PanelAuditEntry>(e => e.Evento == "reporte_decidido" && e.Resultado == severity);
    }

    [Fact]
    public async Task Un_Moderador_No_Decide_Un_Reporte_Que_Lo_Involucra_Spec059_R_07()
    {
        var store = new FakeReports { Parties = new DecidedReportParties("reporter", "moderator-sub", "acoso") };
        var clock = new PanelClock(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero));
        var audit = new FakeAudit();
        var service = new ReportReviewService(store, new RecordingEffects(), new PanelApi(new AuditoriaPanel(audit, new PanelIpHasher(PhoneProtectionTestKeys.Keys()), new NoHttpContext(), clock, NullLogger<AuditoriaPanel>.Instance)), clock);

        var act = () => service.DecideAsync(new ReviewActor("moderator-sub", "s"), _id, new ReportDecisionBody { Gravedad = "critica", Motivo = "motivo suficiente" }, CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Kind.Should().Be(DomainErrorKind.Forbidden);
        store.Decided.Should().BeNull();
    }

    [Fact]
    public async Task Reporte_Inexistente_O_Ya_Decidido_Responde_404_O_409_CA_053_29()
    {
        var store = new FakeReports { Parties = null };
        var clock = new PanelClock(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero));
        var audit = new FakeAudit();
        var service = new ReportReviewService(store, new RecordingEffects(), new PanelApi(new AuditoriaPanel(audit, new PanelIpHasher(PhoneProtectionTestKeys.Keys()), new NoHttpContext(), clock, NullLogger<AuditoriaPanel>.Instance)), clock);
        var body = new ReportDecisionBody { Gravedad = "baja", Motivo = "motivo suficiente" };

        var missing = () => service.DecideAsync(_verifier, _id, body, CancellationToken.None);
        (await missing.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("report_not_found");

        store.Parties = new DecidedReportParties("a", "b", "otro");
        store.Outcome = DecisionStoreOutcome.AlreadyDecided;
        var decided = () => service.DecideAsync(_verifier, _id, body, CancellationToken.None);
        (await decided.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("report_already_decided");
    }

    [Fact]
    public void La_Cola_De_Reportes_Ordena_Critica_Primero_Y_Sin_Gravedad_Al_Final_CA_053_19()
    {
        ReportSeverities.Rank("critica").Should().BeLessThan(ReportSeverities.Rank("alta"));
        ReportSeverities.Rank("baja").Should().BeLessThan(ReportSeverities.Rank(null));
        ReportSeverities.Rank("desconocida").Should().Be(ReportSeverities.All.Count);
    }

    [Fact]
    public void Un_Cursor_Ilegible_Es_400_Y_Uno_Valido_Hace_El_Viaje_De_Ida_Y_Vuelta_CA_053_05()
    {
        var cursor = new ReviewCursor(2, new DateTime(2026, 11, 20, 1, 2, 3, DateTimeKind.Utc), ObjectId.GenerateNewId());

        ReviewCursor.Parse(cursor.Encode()).Should().Be(cursor);
        foreach (var bad in new[] { "!!!", "AAAA", Convert.ToBase64String("x|y|z"u8.ToArray()) })
        {
            var act = () => ReviewCursor.Parse(bad);
            act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_review_cursor");
        }
    }

    [Fact]
    public void El_Rango_Por_Omision_Son_Los_Ultimos_30_Dias_D_08()
    {
        var now = new DateTime(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc);

        ReviewRules.Range(null, null, now).Should().Be((now.AddDays(-30), now));
    }

    [Theory]
    [InlineData("identidad", "en_revision", 20, true)]
    [InlineData("recuperacion", null, 50, true)]
    [InlineData("otro", null, 20, false)]
    [InlineData(null, "estado_raro", 20, false)]
    [InlineData(null, null, 51, false)]
    [InlineData(null, null, 0, false)]
    public void Validador_De_La_Cola_De_Verificaciones_CA_053_04(string? tipo, string? estado, int limite, bool valid)
    {
        new VerificationQueueQueryValidator().Validate(new VerificationQueueQuery { Tipo = tipo, Estado = estado, Limite = limite }).IsValid.Should().Be(valid);
    }

    [Theory]
    [InlineData("aprobada", null, true)]
    [InlineData("rechazada", "foto_ilegible", true)]
    [InlineData("rechazada", null, false)]
    [InlineData("rechazada", "corto", false)]
    [InlineData("aprobada", "corto", false)]
    [InlineData("quizas", null, false)]
    [InlineData(null, null, false)]
    public void Validador_De_La_Decision_De_Verificacion_Exige_Motivo_Al_Rechazar_CA_053_15(string? decision, string? motivo, bool valid)
    {
        new VerificationDecisionBodyValidator().Validate(new VerificationDecisionBody { Decision = decision, Motivo = motivo }).IsValid.Should().Be(valid);
    }

    [Theory]
    [InlineData("critica", "motivo suficiente", true)]
    [InlineData("grave", "motivo suficiente", false)]
    [InlineData("baja", "corto", false)]
    [InlineData("baja", null, false)]
    public void Validador_De_La_Decision_De_Reporte_CA_053_27(string? gravedad, string? motivo, bool valid)
    {
        new ReportDecisionBodyValidator().Validate(new ReportDecisionBody { Gravedad = gravedad, Motivo = motivo }).IsValid.Should().Be(valid);
    }

    [Theory]
    [InlineData("665f1b2c3d4e5f6071829301", true)]
    [InlineData("no-es-un-id", false)]
    public void Validador_Del_Identificador_De_Ruta_CA_053_09(string id, bool valid)
    {
        new ReviewRouteValidator().Validate(new ReviewRoute { Id = id }).IsValid.Should().Be(valid);
    }

    [Fact]
    public async Task Sin_El_Almacen_De_Fotos_De_La_025_Abrir_Falla_Cerrado_Con_503_Spec025_T_04()
    {
        var act = () => new UnavailableVerificationPhotoLinks().IssueAsync(_id, ["selfie"], TimeSpan.FromMinutes(5), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Kind.Should().Be(DomainErrorKind.ServiceUnavailable);
    }
}
