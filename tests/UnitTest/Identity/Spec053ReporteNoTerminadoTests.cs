using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.HomeAndCar.Services;
using Knock.Identity.Api.Modules.Safety.Dtos;
using Knock.Identity.Api.Modules.Safety.Services;
using Knock.Identity.Api.Modules.Safety.Validators;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 053 (BB13): el cliente de la ruta interna de Mercado (token acotado, mapeo de respuestas) y el formato del evento <c>ReporteNoTerminadoCreado</c>. Sin red: un manejador de mentira.</summary>
public sealed class Spec053ReporteNoTerminadoTests
{
    private const string _visit = "0123456789abcdef01234567";
    private static readonly string _key = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 1)).ToArray());

    private static MarketNotFinishedReportClient Client(StubHandler handler, string? key = null, bool withAddress = true) =>
        new(
            handler.Http(withAddress),
            Options.Create(new MarketHistoryOptions { BaseUrl = "http://market", InternalKey = key ?? _key }),
            TimeProvider.System);

    [Fact]
    public async Task Read_SignsAOneMinuteTokenBoundToTheVisitWithTheReportPermission_CA_053_REPORTE_NO_TERMINADO()
    {
        using var handler = new StubHandler(HttpStatusCode.OK);

        var result = await Client(handler).ReadAsync(_visit, CancellationToken.None);

        result.Status.Should().Be(MarketReportStatus.Found);
        handler.Uri!.AbsolutePath.Should().Be($"/api/v1/interno/visitas/{_visit}/reporte-no-terminado");
        var token = new JsonWebTokenHandler().ReadJsonWebToken(handler.Token);
        token.Issuer.Should().Be("knock-identity");
        token.Audiences.Should().Contain("knock-market");
        token.GetClaim("visitaId").Value.Should().Be(_visit);
        token.GetClaim("permisos").Value.Should().Be("reporte-no-terminado");
        (token.ValidTo - token.IssuedAt).Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "NotFound")]
    [InlineData(HttpStatusCode.Forbidden, "Unavailable")]
    [InlineData(HttpStatusCode.Unauthorized, "Unavailable")]
    [InlineData(HttpStatusCode.InternalServerError, "Unavailable")]
    public async Task Read_MapsTheAnswer_CA_053_REPORTE_NO_TERMINADO(HttpStatusCode status, string expected)
    {
        using var handler = new StubHandler(status);

        (await Client(handler).ReadAsync(_visit, CancellationToken.None)).Status.Should().Be(Enum.Parse<MarketReportStatus>(expected));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("corta", true)]
    [InlineData("valida", false)]
    public async Task Read_WithoutAddressOrValidKey_IsUnavailableAndSendsNothing_CA_053_REPORTE_NO_TERMINADO(string? key, bool withAddress)
    {
        using var handler = new StubHandler(HttpStatusCode.OK);
        var client = key == "valida" ? Client(handler, withAddress: withAddress) : Client(handler, key ?? string.Empty);

        (await client.ReadAsync(_visit, CancellationToken.None)).Status.Should().Be(MarketReportStatus.Unavailable);
        handler.Uri.Should().BeNull();
    }

    [Fact]
    public async Task Consume_WhenMarketIsDown_Throws503ForTheSenderToRetry_CA_053_REPORTE_NO_TERMINADO()
    {
        var consumer = new NotFinishedReportConsumer(null!, new FakeMarket(MarketReportResult.Failure(MarketReportStatus.Unavailable)));

        var act = () => consumer.ConsumeAsync(new NotFinishedReportEventBody("e", NotFinishedReportConsumer.EventType, _visit, Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D")), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Kind.Should().Be(DomainErrorKind.ServiceUnavailable);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Validator_AcceptsOnlyIdentifiersOfTheEvent_CA_053_REPORTE_NO_TERMINADO(bool valid, bool expected)
    {
        var body = valid
            ? new NotFinishedReportEventBody("e", NotFinishedReportConsumer.EventType, _visit, Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"))
            : new NotFinishedReportEventBody("e", "OtroEvento", "x", "y", "z");

        new NotFinishedReportEventBodyValidator().Validate(body).IsValid.Should().Be(expected);
    }

    private sealed class FakeMarket(MarketReportResult result) : IMarketNotFinishedReports
    {
        public Task<MarketReportResult> ReadAsync(string visitId, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        private HttpClient? _http;

        public Uri? Uri { get; private set; }

        public HttpClient Http(bool withAddress) => _http = new HttpClient(this, disposeHandler: false) { BaseAddress = withAddress ? new Uri("http://market") : null };

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _http?.Dispose();
            }

            base.Dispose(disposing);
        }

        public string Token { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri is { IsAbsoluteUri: true } absolute ? absolute : new Uri(new Uri("http://market"), request.RequestUri!);
            Token = request.Headers.Authorization!.Parameter!;
            var response = new HttpResponseMessage(status);
            if (status == HttpStatusCode.OK)
            {
                response.Content = JsonContent.Create(new { visitaId = _visit, clienteId = "c", tecnicoUsuarioId = "t", texto = "texto ficticio", creadoEn = DateTime.UtcNow });
            }

            return Task.FromResult(response);
        }
    }
}
