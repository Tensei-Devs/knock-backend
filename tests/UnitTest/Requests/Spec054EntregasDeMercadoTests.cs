using System.Net;
using AwesomeAssertions;
using Knock.Market.Api.Common.Bandeja;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Requests;

/// <summary>Spec 054, R-03: Mercado entrega <c>CotizacionElegida</c> a <c>interno/eventos</c> (spec 048, R-14) y <c>visita_afectada</c> a <c>interno/avisos</c> (specs 037, R-11 y 059, R-11) de Mensajería. Solo datos ficticios.</summary>
public sealed class Spec054EntregasDeMercadoTests
{
    private const string _request = "0123456789abcdef01234567";
    private const string _visit = "76543210fedcba9876543210";

    [Fact]
    public async Task CotizacionElegida_Goes_To_Messaging_Events_With_Only_The_Closed_Body_CA_054_05()
    {
        using var handler = new CapturaDeEntregas(HttpStatusCode.Accepted);
        var message = Chosen();
        var delivery = ChosenDelivery(handler);

        delivery.Tipo.Should().Be("CotizacionElegida");
        (await delivery.DeliverAsync(message, CancellationToken.None)).Should().Be(ResultadoDeEntrega.Aceptado);

        var sent = handler.Sent.Should().ContainSingle().Subject;
        sent.Path.Should().Be("/api/v1/interno/eventos");
        sent.Body.Should().Be($$"""{"eventoId":"{{message.Id}}","tipo":"CotizacionElegida","solicitudId":"{{_request}}","tecnicoUsuarioId":"tech-user"}""");
        CapturaDeEntregas.Claims(sent.Token).Should().Contain(("aud", "knock-notifications")).And.Contain(("permisos", "CotizacionElegida")).And.Contain(("iss", "knock-market"));
    }

    [Fact]
    public async Task VisitaAfectada_Goes_To_Messaging_Notices_With_The_Client_And_The_Visit_CA_054_05()
    {
        using var handler = new CapturaDeEntregas(HttpStatusCode.Accepted);
        var message = Affected();
        var delivery = AffectedDelivery(handler);

        delivery.Tipo.Should().Be("visita_afectada");
        (await delivery.DeliverAsync(message, CancellationToken.None)).Should().Be(ResultadoDeEntrega.Aceptado);

        var sent = handler.Sent.Should().ContainSingle().Subject;
        sent.Path.Should().Be("/api/v1/interno/avisos");
        sent.Body.Should().Be($$"""{"eventoId":"{{message.Id}}","tipo":"visita_afectada","destinatarioId":"client-user","rol":"cliente","recurso":"{{_visit}}"}""");
        CapturaDeEntregas.Claims(sent.Token).Should().Contain(("aud", "knock-notifications")).And.Contain(("permisos", "visita_afectada")).And.Contain(("iss", "knock-market"));
    }

    [Fact]
    public async Task Without_Address_Or_Key_Retries_And_Missing_Data_Is_Final_CA_054_06()
    {
        using var handler = new CapturaDeEntregas(HttpStatusCode.Accepted);
        var empty = new BsonDocument();

        (await ChosenDelivery(handler, configured: false).DeliverAsync(Chosen(), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Reintentable);
        (await AffectedDelivery(handler, configured: false).DeliverAsync(Affected(), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Reintentable);
        (await ChosenDelivery(handler).DeliverAsync(Chosen() with { Datos = empty }, CancellationToken.None)).Should().Be(ResultadoDeEntrega.Rechazado);
        (await AffectedDelivery(handler).DeliverAsync(Affected() with { Datos = empty }, CancellationToken.None)).Should().Be(ResultadoDeEntrega.Rechazado);

        handler.Sent.Should().BeEmpty();
    }

    private static MensajeReservado Chosen() => new($"CotizacionElegida:{_request}", "CotizacionElegida", new BsonDocument
    {
        { "solicitudId", _request },
        { "cotizacionId", _visit },
        { "tecnicoUsuarioId", "tech-user" },
    }, "lease", 0);

    private static MensajeReservado Affected() => new($"visita_afectada:CuentaSuspendida:r:1:{_visit}", "visita_afectada", new BsonDocument
    {
        { "eventoId", $"visita_afectada:CuentaSuspendida:r:1:{_visit}" },
        { "destinatarioId", "client-user" },
        { "rol", "cliente" },
        { "visitaId", _visit },
    }, "lease", 0);

    private static EntregaCotizacionElegida ChosenDelivery(CapturaDeEntregas handler, bool configured = true) =>
        new(handler, CapturaDeEntregas.Messaging(configured), Options.Create(new BandejaOptions()), TimeProvider.System);

    private static EntregaVisitaAfectada AffectedDelivery(CapturaDeEntregas handler, bool configured = true) =>
        new(handler, CapturaDeEntregas.Messaging(configured), Options.Create(new BandejaOptions()), TimeProvider.System);
}
