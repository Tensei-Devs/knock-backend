using System.Net;
using AwesomeAssertions;
using Knock.Contracts.Safety;
using Knock.Identity.Api.Hosting.Procesos.Outbox;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 043 (R-23) y 037 (R-11): la entrega interna HTTP de los eventos de bloqueo, con token firmado por Identidad y permisos por destino. Sin red: un manejador de mentira.</summary>
public sealed class Spec043EntregaInternaTests
{
    private static readonly string _key = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 1)).ToArray());

    [Theory]
    [InlineData(HttpStatusCode.Accepted, "Aceptado")]
    [InlineData(HttpStatusCode.Unauthorized, "Rechazado")]
    [InlineData(HttpStatusCode.Forbidden, "Rechazado")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "Reintentable")]
    [InlineData(HttpStatusCode.TooManyRequests, "Reintentable")]
    public async Task MapsTheAnswerToTheOutboxResult(HttpStatusCode status, string expectedName)
    {
        using var handler = new StubHandler(status);
        var expected = Enum.Parse<ResultadoDeEntrega>(expectedName);
        (await Delivery(handler).DeliverAsync(Event("market"), CancellationToken.None)).Should().Be(expected);
        handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task MarketGetsTheBlocksPermissionAndMessagingGetsTheEventType()
    {
        using var handler = new StubHandler(HttpStatusCode.Accepted);
        var delivery = Delivery(handler);
        await delivery.DeliverAsync(Event("market"), CancellationToken.None);
        await delivery.DeliverAsync(Event("messaging"), CancellationToken.None);

        handler.Requests.Select(r => r.Uri.AbsolutePath).Should().OnlyContain(p => p == "/api/v1/interno/bloqueos");
        Claims(handler.Requests[0].Token).Should().Contain(("aud", "knock-market")).And.Contain(("permisos", "bloqueos")).And.Contain(("iss", "knock-identity"));
        Claims(handler.Requests[1].Token).Should().Contain(("aud", "knock-notifications")).And.Contain(("permisos", BlockCreated.EventType));
        handler.Requests[0].Body.Should().Contain("\"bloqueaId\":\"a\"").And.Contain("\"tipo\":\"BloqueoCreado\"");
    }

    [Fact]
    public async Task TokenLivesAtMostFiveMinutes()
    {
        using var handler = new StubHandler(HttpStatusCode.Accepted);
        await Delivery(handler).DeliverAsync(Event("market"), CancellationToken.None);
        var token = new JsonWebTokenHandler().ReadJsonWebToken(handler.Requests[0].Token);
        (token.ValidTo - token.IssuedAt).Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
        token.GetClaim("jti").Value.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("http://market", null)]
    [InlineData("http://market", "corta")]
    [InlineData("no-es-una-direccion", "key")]
    public async Task WithoutAddressOrValidKeyTheEventIsRetriedAndNothingIsSent(string? address, string? key)
    {
        using var handler = new StubHandler(HttpStatusCode.Accepted);
        var options = new EntregaInternaOptions { Market = new DestinoInterno { BaseUrl = address, InternalKey = key == "key" ? _key : key } };
        (await Delivery(handler, options).DeliverAsync(Event("market"), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Reintentable);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task OtherTypesAndDestinationsStayForRetry()
    {
        using var handler = new StubHandler(HttpStatusCode.Accepted);
        var delivery = Delivery(handler);
        (await delivery.DeliverAsync(new OutboxReservado("x", "TecnicoOcultado", "market", new BsonDocument(), "r", 0), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Reintentable);
        (await delivery.DeliverAsync(Event("identity"), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Reintentable);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SuspensionAndReactivationGoToMarketEventsWithTheEventsPermission()
    {
        using var handler = new StubHandler(HttpStatusCode.Accepted);
        var carga = new BsonDocument { ["eventoId"] = "s1", ["usuarioId"] = "u1", ["reporteId"] = "r1", ["desde"] = "2026-11-22T12:00:00.0000000Z" };
        (await Delivery(handler).DeliverAsync(new OutboxReservado("CuentaSuspendida:u1:1", "CuentaSuspendida", "market", carga, "r", 0), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Aceptado);

        handler.Requests[0].Uri.AbsolutePath.Should().Be("/api/v1/interno/eventos");
        Claims(handler.Requests[0].Token).Should().Contain(("aud", "knock-market")).And.Contain(("permisos", "eventos"));
        handler.Requests[0].Body.Should().Contain("\"tipo\":\"CuentaSuspendida\"").And.Contain("\"usuarioId\":\"u1\"");
    }

    [Fact]
    public async Task AccountDeletedGoesToMarketBoundToThePerson()
    {
        using var handler = new StubHandler(HttpStatusCode.Accepted);
        await Delivery(handler).DeliverAsync(new OutboxReservado("CuentaEliminada:u 1:1", "CuentaEliminada", "market", new BsonDocument { ["sub"] = "u 1" }, "r", 0), CancellationToken.None);

        handler.Requests[0].Uri.AbsolutePath.Should().Be("/api/v1/interno/cuentas/u%201/eliminada");
        Claims(handler.Requests[0].Token).Should().Contain(("permisos", "cuenta")).And.Contain(("usuarioId", "u 1"));
    }

    [Fact]
    public async Task RevokedSessionGoesToMessagingNoticesWithTheTypeAsPermission()
    {
        using var handler = new StubHandler(HttpStatusCode.Accepted);
        await Delivery(handler).DeliverAsync(new OutboxReservado("sesion_revocada:s1:1", "sesion_revocada", "messaging", new BsonDocument { ["sid"] = "s1" }, "r", 0), CancellationToken.None);

        handler.Requests[0].Uri.AbsolutePath.Should().Be("/api/v1/interno/avisos");
        Claims(handler.Requests[0].Token).Should().Contain(("aud", "knock-notifications")).And.Contain(("permisos", "sesion_revocada"));
        handler.Requests[0].Body.Should().Contain("\"sid\":\"s1\"");
    }

    [Fact]
    public async Task ReportDecidedGoesToMessagingNoticesAsAFixedNoticeWithoutReportData_CA_037_20()
    {
        using var handler = new StubHandler(HttpStatusCode.Accepted);
        var carga = new BsonDocument { ["eventoId"] = "d1", ["usuarioId"] = "u1", ["reporteId"] = "507f1f77bcf86cd799439011", ["rol"] = "reportado", ["accion"] = "Suspend" };

        (await Delivery(handler).DeliverAsync(new OutboxReservado("ReporteDecidido:r:reportado:1", "ReporteDecidido", "messaging", carga, "r", 0), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Aceptado);

        handler.Requests[0].Uri.AbsolutePath.Should().Be("/api/v1/interno/avisos");
        Claims(handler.Requests[0].Token).Should().Contain(("aud", "knock-notifications")).And.Contain(("permisos", "reporte_decidido"));
        handler.Requests[0].Body.Should().Contain("\"tipo\":\"reporte_decidido\"").And.Contain("\"destinatarioId\":\"u1\"").And.Contain("\"recurso\":\"507f1f77bcf86cd799439011\"")
            .And.NotContain("Suspend").And.NotContain("\"rol\"").And.NotContain("accion");
    }

    [Fact]
    public async Task ExportReadyGoesToMessagingNoticesAsAFixedNoticeWithoutALink_CA_039_35()
    {
        using var handler = new StubHandler(HttpStatusCode.Accepted);
        var carga = new BsonDocument { ["eventoId"] = "exportacion_lista:k:1", ["usuarioId"] = "u1", ["recurso"] = "507f1f77bcf86cd799439011" };

        (await Delivery(handler).DeliverAsync(new OutboxReservado("exportacion_lista:k:1", "exportacion_lista", "messaging", carga, "r", 0), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Aceptado);

        handler.Requests[0].Uri.AbsolutePath.Should().Be("/api/v1/interno/avisos");
        Claims(handler.Requests[0].Token).Should().Contain(("aud", "knock-notifications")).And.Contain(("permisos", "exportacion_lista"));
        handler.Requests[0].Body.Should().Contain("\"tipo\":\"exportacion_lista\"").And.Contain("\"destinatarioId\":\"u1\"").And.Contain("\"recurso\":\"507f1f77bcf86cd799439011\"")
            .And.NotContain("http").And.NotContain("enlace");
    }

    [Fact]
    public async Task NetworkFailureIsRetriable()
    {
        using var handler = new StubHandler(null);
        (await Delivery(handler).DeliverAsync(Event("market"), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Reintentable);
    }

    private static OutboxReservado Event(string destination) => new(
        $"BloqueoCreado:1:{destination}:1",
        BlockCreated.EventType,
        destination,
        new BsonDocument { ["eventoId"] = "e1", ["bloqueaId"] = "a", ["bloqueadoId"] = "b", ["ocurridoEn"] = "2026-11-22T12:00:00.0000000Z" },
        "r",
        0);

    private static EntregaInternaHttp Delivery(StubHandler handler, EntregaInternaOptions? options = null) =>
        new(new StubFactory(handler), Options.Create(options ?? new EntregaInternaOptions
        {
            Market = new DestinoInterno { BaseUrl = "http://market", InternalKey = _key },
            Messaging = new DestinoInterno { BaseUrl = "http://messaging", InternalKey = _key },
        }), TimeProvider.System);

    private static IEnumerable<(string Type, string Value)> Claims(string token) =>
        new JsonWebTokenHandler().ReadJsonWebToken(token).Claims.Select(c => (c.Type, c.Value));

    private sealed record Sent(Uri Uri, string Token, string Body);

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHandler(HttpStatusCode? status) : HttpMessageHandler
    {
        public List<Sent> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new Sent(request.RequestUri!, request.Headers.Authorization!.Parameter!, await request.Content!.ReadAsStringAsync(cancellationToken)));
            return status is { } code ? new HttpResponseMessage(code) : throw new HttpRequestException("down");
        }
    }
}
