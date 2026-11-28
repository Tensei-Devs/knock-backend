using AwesomeAssertions;
using Knock.Contracts.Moderation;
using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Common.TareasProgramadas;
using Knock.Market.Api.Modules.Media;
using Knock.Market.Api.Modules.Reviews.Data;
using Knock.Market.Api.Modules.Reviews.Domain;
using Knock.Market.Api.Modules.Reviews.Dtos;
using Knock.Market.Api.Modules.Reviews.Services;
using Knock.Market.Api.Modules.Reviews.Validators;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Reviews;

/// <summary>Spec 051: validadores, niveles de reputación y reglas del servicio de opiniones (R-02 a R-17). Solo datos ficticios.</summary>
public sealed class Spec051ReviewsTests : IDisposable
{
    private const string _client = "client-1";
    private static readonly DateTime _now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly FakeStore _store = new();
    private readonly FakeModerator _moderator = new();

    public void Dispose() => _cache.Dispose();

    private ReviewService Service() => new(_store, _moderator, new FakeMedia(), _cache, new FixedClock());

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void Calificacion_Va_De_1_A_5_CA_051_20(int stars, bool valid) => new CreateReviewBodyValidator().Validate(new CreateReviewBody(stars, null, null, null, null)).IsValid.Should().Be(valid);

    [Fact]
    public void Comentario_De_Mas_De_500_Caracteres_O_4_Fotos_O_Parte_Fuera_De_Rango_Se_Rechazan_CA_051_20()
    {
        var validator = new CreateReviewBodyValidator();
        var photos = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid().ToString("D")).ToList();

        validator.Validate(new CreateReviewBody(5, null, new string('a', 501), null, null)).IsValid.Should().BeFalse();
        validator.Validate(new CreateReviewBody(5, null, null, photos, null)).IsValid.Should().BeFalse();
        validator.Validate(new CreateReviewBody(5, new ReviewParts(6, null, null, null), null, null, null)).IsValid.Should().BeFalse();
        validator.Validate(new CreateReviewBody(5, new ReviewParts(5, 4, 3, 2), new string('a', 500), photos.Take(3).ToList(), true)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("Gracias por su comentario", true)]
    public void Respuesta_Es_Obligatoria_De_1_A_500_CA_051_16(string? text, bool valid) => new AnswerReviewBodyValidator().Validate(new AnswerReviewBody(text)).IsValid.Should().Be(valid);

    [Theory]
    [InlineData(20, "4.7", "destacado")]
    [InlineData(20, "4.6", "confiable")]
    [InlineData(5, "4.2", "confiable")]
    [InlineData(5, "3.9", "nuevo")]
    [InlineData(4, "5.0", "nuevo")]
    public void Nivel_Sale_Del_Total_Y_El_Promedio_CA_051_21(int total, string average, string level) =>
        ReviewRules.LevelOf(total, double.Parse(average, System.Globalization.CultureInfo.InvariantCulture)).Should().Be(level);

    [Fact]
    public void Promedio_Lleva_Un_Decimal_Y_Sin_Opiniones_Es_Nulo_CA_051_21()
    {
        ReviewRules.AverageOf(new Dictionary<int, int> { [1] = 0, [2] = 0, [3] = 0, [4] = 4, [5] = 1 }).Should().Be(4.2);
        ReviewRules.AverageOf(new Dictionary<int, int> { [1] = 0, [2] = 0, [3] = 0, [4] = 0, [5] = 0 }).Should().BeNull();
    }

    [Fact]
    public void Plazo_De_30_Dias_Se_Cuenta_Desde_Que_Termina_CA_051_19()
    {
        ReviewRules.IsWithinPeriod(_now, _now.AddDays(-30)).Should().BeTrue();
        ReviewRules.IsWithinPeriod(_now, _now.AddDays(-30).AddSeconds(-1)).Should().BeFalse();
    }

    [Fact]
    public void Cursor_Se_Codifica_Y_Un_Cursor_Ajeno_No_Se_Acepta_CA_051_05()
    {
        var id = ObjectId.GenerateNewId();

        ReviewFormat.TryDecodeCursor(ReviewFormat.EncodeCursor(_now, id), out var at, out var back).Should().BeTrue();
        at.Should().Be(_now);
        back.Should().Be(id);
        ReviewFormat.TryDecodeCursor("no-es-un-cursor", out _, out _).Should().BeFalse();
        new TechnicianReviewsQueryValidator().Validate(new TechnicianReviewsQuery { Id = id.ToString(), Limite = 51 }).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Cliente_Dueno_De_Visita_Terminada_Crea_La_Opinion_CA_051_19()
    {
        var result = await Service().CreateAsync(_client, _store.Visit.Id.ToString(), new CreateReviewBody(5, null, "Excelente trabajo", null, true), CancellationToken.None);

        result.Moderacion.Should().Be("publicada");
        _store.Created.Should().ContainSingle().Which.SaveAsTrusted.Should().BeTrue();
    }

    [Fact]
    public async Task Persona_Ajena_A_La_Visita_Responde_403_CA_051_09()
    {
        var act = () => Service().CreateAsync("otra-persona", _store.Visit.Id.ToString(), new CreateReviewBody(5, null, null, null, null), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Kind.Should().Be(DomainErrorKind.Forbidden);
        _store.Created.Should().BeEmpty();
    }

    [Theory]
    [InlineData("en_curso", 1)]
    [InlineData("terminada", 31)]
    public async Task Visita_No_Terminada_O_Fuera_De_Plazo_Responde_409_CA_051_12(string state, int daysSinceFinished)
    {
        _store.Visit = _store.Visit with { State = state, FinishedAtUtc = _now.AddDays(-daysSinceFinished) };

        var act = () => Service().CreateAsync(_client, _store.Visit.Id.ToString(), new CreateReviewBody(5, null, null, null, null), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be(ReviewRules.ReviewNotAllowedCode);
    }

    [Fact]
    public async Task Segunda_Opinion_De_La_Misma_Visita_Responde_409_CA_051_28()
    {
        _store.Duplicate = true;

        var act = () => Service().CreateAsync(_client, _store.Visit.Id.ToString(), new CreateReviewBody(5, null, null, null, null), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be(ReviewRules.ReviewNotAllowedCode);
    }

    [Theory]
    [InlineData(ModerationOutcome.Allowed, "publicada", true)]
    [InlineData(ModerationOutcome.Unavailable, "pendiente", true)]
    [InlineData(ModerationOutcome.Rejected, "rechazada", false)]
    public async Task Comentario_Se_Modera_Y_La_Calificacion_Cuenta_De_Todos_Modos_CA_051_24(ModerationOutcome outcome, string state, bool keepsComment)
    {
        _moderator.Outcome = outcome;

        var result = await Service().CreateAsync(_client, _store.Visit.Id.ToString(), new CreateReviewBody(4, null, "Buen servicio", null, null), CancellationToken.None);

        result.Moderacion.Should().Be(state);
        (_store.Created[0].Comment is not null).Should().Be(keepsComment);
    }

    [Fact]
    public async Task Tecnico_Ajeno_No_Responde_Y_La_Segunda_Respuesta_Es_409_CA_051_22()
    {
        var review = new StoredReview(ObjectId.GenerateNewId(), _store.Visit.TechnicianId, false);
        _store.Review = review;
        _store.TechnicianOfUser = ObjectId.GenerateNewId();

        var foreign = () => Service().AnswerAsync("tecnico-2", review.Id.ToString(), new AnswerReviewBody("Gracias"), CancellationToken.None);
        (await foreign.Should().ThrowAsync<DomainException>()).Which.Kind.Should().Be(DomainErrorKind.Forbidden);

        _store.TechnicianOfUser = review.TechnicianId;
        _store.Review = review with { Answered = true };
        var again = () => Service().AnswerAsync("tecnico-1", review.Id.ToString(), new AnswerReviewBody("Gracias"), CancellationToken.None);
        (await again.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be(ReviewRules.ReviewAlreadyAnsweredCode);
    }

    [Fact]
    public async Task Consulta_Repetida_Sale_De_La_Cache_Y_La_Base_Recibe_Una_Sola_CA_051_06()
    {
        var service = Service();
        var id = ObjectId.GenerateNewId().ToString();

        var first = await service.ListAsync(id, null, 20, CancellationToken.None);
        var second = await service.ListAsync(id, null, 20, CancellationToken.None);

        second.Should().BeSameAs(first);
        _store.Lists.Should().Be(1);
    }

    [Fact]
    public async Task Tecnico_Oculto_Responde_404_CA_051_04()
    {
        _store.Visible = false;

        var act = () => Service().ListAsync(ObjectId.GenerateNewId().ToString(), null, 20, CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be(ReviewRules.TechnicianNotFound);
    }

    [Fact]
    public async Task Tarea_CerrarPeriodoDeOpinion_Cierra_El_Plazo_De_La_Visita_Y_Rechaza_Una_Clave_Invalida()
    {
        var handler = new CerrarPeriodoDeOpinion(_store, new FixedClock());
        var id = ObjectId.GenerateNewId();

        await handler.HandleAsync(new TareaReservada(ObjectId.GenerateNewId(), handler.Tipo, id.ToString(), "token", 0), CancellationToken.None);
        var bad = () => handler.HandleAsync(new TareaReservada(ObjectId.GenerateNewId(), handler.Tipo, "x", "token", 0), CancellationToken.None);

        _store.Closed.Should().Equal(id);
        await bad.Should().ThrowAsync<FormatException>();
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class FakeModerator : IContentModerator
    {
        public ModerationOutcome Outcome { get; set; } = ModerationOutcome.Allowed;

        public Task<ModerationResult> ReviewTextAsync(string text, CancellationToken cancellationToken) => Task.FromResult(new ModerationResult(Outcome));

        public Task<ModerationResult> ReviewImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) => Task.FromResult(new ModerationResult(Outcome));
    }

    private sealed class FakeMedia : IMediaApi
    {
        public Task<bool> IsReadyAndOwnedByAsync(Guid fileId, string ownerId, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> IsReadyOwnedAndUsedForAsync(Guid fileId, string ownerId, string use, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<IReadOnlyList<(Guid FileId, Uri Url, DateTimeOffset ExpiresAt)>> CreateReadLinksAsync(IReadOnlyList<Guid> fileIds, string use, string requesterSub, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<(Guid FileId, Uri Url, DateTimeOffset ExpiresAt)>>([]);
    }

    private sealed class FakeStore : IReviewStore
    {
        public Task AnonymizeByAuthorAsync(string clientId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ReviewVisit Visit { get; set; } = new(ObjectId.GenerateNewId(), ObjectId.GenerateNewId(), _client, ObjectId.GenerateNewId(), "terminada", _now.AddDays(-2));

        public StoredReview? Review { get; set; }

        public ObjectId? TechnicianOfUser { get; set; }

        public bool Duplicate { get; set; }

        public bool Visible { get; set; } = true;

        public int Lists { get; private set; }

        public List<NewReview> Created { get; } = [];

        public List<ObjectId> Closed { get; } = [];

        public Task<ReviewVisit?> FindVisitAsync(ObjectId visitId, CancellationToken cancellationToken) => Task.FromResult<ReviewVisit?>(Visit);

        public Task<bool> IsTechnicianVisibleAsync(ObjectId technicianId, CancellationToken cancellationToken) => Task.FromResult(Visible);

        public Task<ObjectId?> FindTechnicianByUserAsync(string sub, CancellationToken cancellationToken) => Task.FromResult(TechnicianOfUser);

        public Task<StoredReview?> FindReviewAsync(ObjectId id, CancellationToken cancellationToken) => Task.FromResult(Review);

        public Task<ObjectId?> CreateAsync(NewReview review, DateTime nowUtc, CancellationToken cancellationToken)
        {
            if (Duplicate)
            {
                return Task.FromResult<ObjectId?>(null);
            }

            Created.Add(review);
            return Task.FromResult<ObjectId?>(ObjectId.GenerateNewId());
        }

        public Task<bool> AnswerAsync(StoredReview review, string text, DateTime nowUtc, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<ReviewPage> ListAsync(ObjectId technicianId, (DateTime CreatedAtUtc, ObjectId Id)? cursor, int limit, CancellationToken cancellationToken)
        {
            Lists++;
            return Task.FromResult(new ReviewPage([], new ReviewSummary(null, 0, new Dictionary<string, int>()), null));
        }

        public Task<ReviewModerationResult> SetHiddenAsync(ObjectId id, bool hidden, string? reason, string moderatorId, DateTime nowUtc, CancellationToken cancellationToken) =>
            Task.FromResult(new ReviewModerationResult(ReviewModerationOutcome.Unchanged, null));

        public Task CloseOpinionPeriodAsync(ObjectId visitId, DateTime nowUtc, CancellationToken cancellationToken)
        {
            Closed.Add(visitId);
            return Task.CompletedTask;
        }
    }
}
