using System.Net;
using AwesomeAssertions;
using Knock.Market.Api.Common.Bandeja;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Requests;

/// <summary>
/// Spec 037 (R-11 y §4) y spec 054 (R-03): Mercado entrega a <c>interno/avisos</c> los avisos de solicitudes de las specs 020, 021, 028 y 029 con la persona, el rol y el recurso, y nada más. Solo datos
/// ficticios.
/// </summary>
public sealed class Spec037AvisosDeMercadoTests
{
    private const string _request = "0123456789abcdef01234567";
    private const string _quote = "76543210fedcba9876543210";
    private const string _technician = "aaaaaaaaaaaaaaaaaaaaaaaa";
    private const string _otherTechnician = "bbbbbbbbbbbbbbbbbbbbbbbb";
    private const string _visit = "cccccccccccccccccccccccc";

    [Theory]
    [InlineData("SolicitudEnviadaATecnico", "solicitud_nueva", "invited-user", "tecnico", _request)]
    [InlineData("SolicitudSinTecnicos", "solicitud_sin_tecnicos", "client-user", "cliente", _request)]
    [InlineData("CotizacionEnviada", "cotizacion_nueva", "client-user", "cliente", _request)]
    [InlineData("CotizacionAceptada", "cotizacion_aceptada", "chosen-user", "tecnico", _visit)]
    [InlineData("SolicitudCancelada", "solicitud_cancelada", "quoting-user", "tecnico", _request)]
    [InlineData("SolicitudVencida", "solicitud_vencida", "owner-user", "cliente", _request)]
    public async Task Cada_Evento_Llega_A_Su_Persona_Con_El_Aviso_Del_Catalogo_CA_054_05(string eventType, string notice, string person, string role, string resource)
    {
        using var handler = new CapturaDeEntregas(HttpStatusCode.Accepted);
        var message = Message(eventType, Data(_technician));

        (await Delivery(eventType, handler).DeliverAsync(message, CancellationToken.None)).Should().Be(ResultadoDeEntrega.Aceptado);

        var sent = handler.Sent.Should().ContainSingle().Subject;
        sent.Path.Should().Be("/api/v1/interno/avisos");
        sent.Body.Should().Be($$"""{"eventoId":"{{message.Id}}","tipo":"{{notice}}","destinatarioId":"{{person}}","rol":"{{role}}","recurso":"{{resource}}"}""");
        CapturaDeEntregas.Claims(sent.Token).Should().Contain(("aud", "knock-notifications")).And.Contain(("permisos", notice)).And.Contain(("iss", "knock-market"));
    }

    [Fact]
    public void No_Se_Inventa_La_Cotizacion_Vencida_CA_054_05() =>
        AvisosDeSolicitud.All.Select(x => x.NoticeType).Should().BeEquivalentTo(
            "solicitud_nueva", "solicitud_sin_tecnicos", "cotizacion_nueva", "cotizacion_aceptada", "solicitud_cancelada", "solicitud_vencida");

    [Fact]
    public async Task La_Cancelacion_Avisa_A_Cada_Tecnico_Y_Sin_Ninguno_No_Envia_CA_054_05()
    {
        using var handler = new CapturaDeEntregas(HttpStatusCode.Accepted);
        var delivery = Delivery("SolicitudCancelada", handler);

        (await delivery.DeliverAsync(Message("SolicitudCancelada", Data()), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Aceptado);
        handler.Sent.Should().BeEmpty();

        (await delivery.DeliverAsync(Message("SolicitudCancelada", Data(_technician, _otherTechnician)), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Aceptado);
        handler.Sent.Should().HaveCount(2);
    }

    [Fact]
    public async Task Un_Tecnico_Sin_Persona_No_Impide_Los_Demas_Y_Queda_Fallido_CA_054_06()
    {
        using var handler = new CapturaDeEntregas(HttpStatusCode.Accepted);

        var result = await Delivery("SolicitudCancelada", handler).DeliverAsync(Message("SolicitudCancelada", Data("ffffffffffffffffffffffff", _technician)), CancellationToken.None);

        result.Should().Be(ResultadoDeEntrega.Rechazado);
        handler.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task Sin_Datos_Es_Definitivo_Y_Sin_Destino_Se_Reintenta_CA_054_06()
    {
        using var handler = new CapturaDeEntregas(HttpStatusCode.ServiceUnavailable);
        foreach (var notice in AvisosDeSolicitud.All)
        {
            (await Delivery(notice.EventType, handler).DeliverAsync(Message(notice.EventType, new BsonDocument()), CancellationToken.None))
                .Should().Be(ResultadoDeEntrega.Rechazado, notice.EventType);
        }

        handler.Sent.Should().BeEmpty();
        var full = Message("CotizacionEnviada", Data(_technician));
        (await Delivery("CotizacionEnviada", handler, configured: false).DeliverAsync(full, CancellationToken.None)).Should().Be(ResultadoDeEntrega.Reintentable);
        (await Delivery("SolicitudCancelada", handler).DeliverAsync(Message("SolicitudCancelada", Data(_technician, _otherTechnician)), CancellationToken.None))
            .Should().Be(ResultadoDeEntrega.Reintentable);
        handler.Sent.Should().ContainSingle("al primer fallo se reprograma el mensaje completo");
    }

    private static MensajeReservado Message(string eventType, BsonDocument data) => new($"{eventType}:{_request}", eventType, data, "lease", 0);

    private static BsonDocument Data(params string[] technicians)
    {
        var data = new BsonDocument
        {
            { "solicitudId", _request },
            { "cotizacionId", _quote },
            { "clienteId", "owner-user" },
            { "tecnicoUsuarioId", "chosen-user" },
            { "tecnicosAvisar", new BsonArray(technicians) },
        };
        if (technicians.Length > 0)
        {
            data.Add("tecnicoId", technicians[0]);
        }

        return data;
    }

    private static EntregaAvisoDeSolicitud Delivery(string eventType, CapturaDeEntregas handler, bool configured = true) =>
        new(AvisosDeSolicitud.All.Single(x => x.EventType == eventType), new People(), handler, CapturaDeEntregas.Messaging(configured), Options.Create(new BandejaOptions()), TimeProvider.System);

    private sealed class People : IDestinatariosDeAviso
    {
        public Task<string?> ClientAsync(string requestId, CancellationToken cancellationToken) => Found(requestId == _request, "client-user");

        public Task<string?> InvitedTechnicianAsync(string requestId, string technicianId, CancellationToken cancellationToken) => Found(technicianId == _technician, "invited-user");

        public Task<string?> QuotingTechnicianAsync(string requestId, string technicianId, CancellationToken cancellationToken) =>
            Found(technicianId is _technician or _otherTechnician, "quoting-user");

        public Task<string?> VisitAsync(string requestId, string quoteId, CancellationToken cancellationToken) => Found(quoteId == _quote, _visit);

        private static Task<string?> Found(bool found, string value) => Task.FromResult(found ? value : null);
    }
}
