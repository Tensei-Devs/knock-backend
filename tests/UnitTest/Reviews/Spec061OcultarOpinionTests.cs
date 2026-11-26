using System.Net;
using AwesomeAssertions;
using Knock.Market.Api.Common.Bandeja;
using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Modules.Reviews.Data;
using Knock.Market.Api.Modules.Reviews.Domain;
using Knock.Market.Api.Modules.Reviews.Dtos;
using Knock.Market.Api.Modules.Reviews.Services;
using Knock.Market.Api.Modules.Reviews.Validators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Reviews;

/// <summary>Spec 061: motivo de ocultar, servicio de moderación (generación de la caché) y entrega de <c>OpinionModerada</c> a Identidad y Mensajería. Solo datos ficticios.</summary>
public sealed class Spec061OcultarOpinionTests
{
    private static readonly string _key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(300, true)]
    [InlineData(301, false)]
    public void Motivo_De_8_A_300_Caracteres_CA_061_06(int length, bool valid) =>
        new HideReviewBodyValidator().Validate(new HideReviewBody(new string('a', length))).IsValid.Should().Be(valid);

    [Fact]
    public void Motivo_Vacio_O_Con_Controles_Se_Rechaza_Sin_Repetir_El_Valor_CA_061_06()
    {
        var validator = new HideReviewBodyValidator();

        validator.Validate(new HideReviewBody(null)).IsValid.Should().BeFalse();
        var withControl = validator.Validate(new HideReviewBody("motivo\u0007valido"));
        withControl.IsValid.Should().BeFalse();
        withControl.Errors.Should().OnlyContain(e => !e.ErrorMessage.Contains("motivo", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Opinion_Inexistente_Responde_404_review_not_found_CA_061_07()
    {
        var service = new ReviewModerationService(new FakeStore(ReviewModerationOutcome.NotFound), TimeProvider.System);

        var act = () => service.HideAsync("sub", ObjectId.GenerateNewId(), "motivo valido", CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("review_not_found");
    }

    [Fact]
    public async Task Un_Cambio_Sube_La_Generacion_De_La_Cache_Y_Sin_Cambio_No_CA_061_01_CA_061_03()
    {
        var technician = ObjectId.GenerateNewId();
        var before = ReviewService.CacheKey(technician, null, 20);

        await new ReviewModerationService(new FakeStore(ReviewModerationOutcome.Unchanged, technician), TimeProvider.System).HideAsync("sub", ObjectId.GenerateNewId(), "motivo valido", CancellationToken.None);
        ReviewService.CacheKey(technician, null, 20).Should().Be(before);

        await new ReviewModerationService(new FakeStore(ReviewModerationOutcome.Changed, technician), TimeProvider.System).ShowAsync("sub", ObjectId.GenerateNewId(), CancellationToken.None);
        ReviewService.CacheKey(technician, null, 20).Should().NotBe(before);
        ReviewService.CacheKey(technician, "otro-cursor", 5).Should().Contain($":{ReviewCacheGenerations.Current(technician)}:");
    }

    [Fact]
    public async Task Ocultada_Va_A_Identidad_Y_A_Mensajeria_Con_Solo_Identificadores_CA_061_09_CA_061_09a()
    {
        using var handler = new Capture(HttpStatusCode.Accepted);
        var message = Message("ocultada");

        (await Delivery(handler).DeliverAsync(message, CancellationToken.None)).Should().Be(ResultadoDeEntrega.Aceptado);

        handler.Sent.Select(x => x.Path).Should().Equal("/api/v1/interno/opiniones-moderadas", "/api/v1/interno/avisos");
        var identity = Claims(handler.Sent[0].Token);
        identity.Should().Contain(("aud", "knock-identity")).And.Contain(("permisos", "opinion-moderada"));
        var notice = Claims(handler.Sent[1].Token);
        notice.Should().Contain(("aud", "knock-notifications")).And.Contain(("permisos", "opinion_oculta")).And.Contain(("iss", "knock-market"));
        handler.Sent[1].Body.Should().Contain("\"tipo\":\"opinion_oculta\"").And.Contain("\"rol\":\"tecnico\"").And.Contain("\"destinatarioId\":\"tech-user\"");
        foreach (var sent in handler.Sent)
        {
            sent.Body.Should().NotContain("motivo").And.NotContain("comentario").And.NotContain("calificacion");
        }
    }

    [Fact]
    public async Task Mostrada_Solo_Va_A_Identidad_Y_Sin_Clave_O_Destino_Queda_Para_Reintento_CA_061_09a()
    {
        using var shown = new Capture(HttpStatusCode.Accepted);
        using var unavailable = new Capture(HttpStatusCode.ServiceUnavailable);
        using var rejected = new Capture(HttpStatusCode.Forbidden);
        using var none = new Capture(HttpStatusCode.Accepted);

        (await Delivery(shown).DeliverAsync(Message("mostrada"), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Aceptado);
        (await Delivery(unavailable).DeliverAsync(Message("ocultada"), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Reintentable);
        (await Delivery(rejected).DeliverAsync(Message("ocultada"), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Rechazado);
        (await Delivery(none, withMessaging: false).DeliverAsync(Message("ocultada"), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Reintentable);

        shown.Sent.Should().ContainSingle();
        unavailable.Sent.Should().ContainSingle();
    }

    private static MensajeReservado Message(string action) =>
        new($"OpinionModerada:{ObjectId.GenerateNewId()}:1", "OpinionModerada", new BsonDocument
        {
            { "opinionId", ObjectId.GenerateNewId().ToString() },
            { "tecnicoId", ObjectId.GenerateNewId().ToString() },
            { "tecnicoUsuarioId", "tech-user" },
            { "accion", action },
            { "porId", Guid.NewGuid().ToString("D") },
        }, "lease", 0);

    private static EntregaOpinionModerada Delivery(Capture handler, bool withMessaging = true)
    {
        var settings = new Dictionary<string, string?> { ["Services:Identity:BaseUrl"] = "http://identity.test", ["Security:InternalTokens:MarketKey"] = _key };
        if (withMessaging)
        {
            settings["Services:Messaging:BaseUrl"] = "http://messaging.test";
            settings["Services:Messaging:InternalKey"] = _key;
        }

        return new EntregaOpinionModerada(new Factory(handler), new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), Options.Create(new BandejaOptions()), TimeProvider.System);
    }

    private static List<(string Type, string Value)> Claims(string token) =>
        [.. new JsonWebTokenHandler().ReadJsonWebToken(token).Claims.Select(c => (c.Type, c.Value))];

    private sealed record Sent(string Path, string Body, string Token);

    private sealed class Capture(HttpStatusCode answer) : HttpMessageHandler
    {
        public List<Sent> Sent { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sent.Add(new Sent(request.RequestUri!.AbsolutePath, await request.Content!.ReadAsStringAsync(cancellationToken), request.Headers.Authorization!.Parameter!));
            return new HttpResponseMessage(answer);
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class FakeStore(ReviewModerationOutcome outcome, ObjectId? technician = null) : IReviewStore
    {
        public Task<ReviewModerationResult> SetHiddenAsync(ObjectId id, bool hidden, string? reason, string moderatorId, DateTime nowUtc, CancellationToken cancellationToken) =>
            Task.FromResult(new ReviewModerationResult(outcome, technician));

        public Task<ReviewVisit?> FindVisitAsync(ObjectId visitId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> IsTechnicianVisibleAsync(ObjectId technicianId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ObjectId?> FindTechnicianByUserAsync(string sub, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StoredReview?> FindReviewAsync(ObjectId id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ObjectId?> CreateAsync(NewReview review, DateTime nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> AnswerAsync(StoredReview review, string text, DateTime nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ReviewPage> ListAsync(ObjectId technicianId, (DateTime CreatedAtUtc, ObjectId Id)? cursor, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task CloseOpinionPeriodAsync(ObjectId visitId, DateTime nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
