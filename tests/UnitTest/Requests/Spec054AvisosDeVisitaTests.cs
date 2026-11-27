using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Knock.Market.Api.Common.Bandeja;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Requests;

/// <summary>
/// Spec 054 y spec 049 (enmiendas del 2026-11-27): cada evento de la visita, de la cotización final y del ajuste de precio llega solo a las personas indicadas, con la visita como recurso y sin nada
/// más que identificadores. Solo datos ficticios.
/// </summary>
public sealed class Spec054AvisosDeVisitaTests
{
    private const string _visit = "cccccccccccccccccccccccc";
    private const string _client = "client-user";
    private const string _technician = "technician-user";

    [Theory]
    [InlineData("ReprogramacionPropuesta", "cliente", "reprogramacion_propuesta:tecnico")]
    [InlineData("ReprogramacionPropuesta", "tecnico", "reprogramacion_propuesta:cliente")]
    [InlineData("VisitaReprogramada", null, "visita_reprogramada:cliente,visita_reprogramada:tecnico")]
    [InlineData("ReprogramacionRechazada", "cliente", "reprogramacion_sin_cambio:cliente")]
    [InlineData("ReprogramacionVencida", "tecnico", "reprogramacion_sin_cambio:tecnico")]
    [InlineData("CancelacionTardia", null, "visita_cancelada:tecnico")]
    [InlineData("ClienteAusente", null, "cliente_ausente:cliente")]
    [InlineData("VisitaTerminada", null, "visita_terminada:cliente")]
    public async Task Cada_Evento_De_La_Visita_Avisa_Solo_A_Quien_Corresponde_CA_054_23(string eventType, string? proposedBy, string expected) =>
        await AssertNoticesAsync(AvisosDeVisita.All, eventType, Data(proposedBy), expected);

    [Theory]
    [InlineData("CotizacionFinalEnviada", "cotizacion_final_nueva:cliente")]
    [InlineData("CotizacionFinalDecidida", "cotizacion_final_respondida:tecnico")]
    [InlineData("CotizacionFinalVencida", "cotizacion_final_respondida:tecnico")]
    [InlineData("AjustePrecioPropuesto", "ajuste_precio_nuevo:cliente")]
    [InlineData("AjustePrecioDecidido", "ajuste_precio_respondido:tecnico")]
    [InlineData("AjustePrecioVencido", "ajuste_precio_respondido:tecnico")]
    public async Task Cada_Evento_De_Precio_Avisa_Solo_A_Quien_Corresponde_CA_049_35_CA_049_36(string eventType, string expected)
    {
        // Los dos eventos de respuesta del ajuste no llevan personas: el técnico se lee de la visita.
        var data = eventType is "AjustePrecioDecidido" or "AjustePrecioVencido"
            ? new BsonDocument { { "ajusteId", "a" }, { "cotizacionId", "q" }, { "visitaId", _visit }, { "decision", "aprobado" } }
            : Data(null);

        await AssertNoticesAsync(AvisosDePrecio.All, eventType, data, expected);
    }

    [Theory]
    [InlineData("ReprogramacionPropuesta")]
    [InlineData("ReprogramacionRechazada")]
    [InlineData("ReprogramacionVencida")]
    public async Task Sin_Quien_Propuso_El_Mensaje_Queda_Fallido_Y_No_Envia_CA_054_23(string eventType)
    {
        using var handler = new CapturaDeEntregas(HttpStatusCode.Accepted);

        var result = await DeliverAsync(AvisosDeVisita.All, eventType, Data(null), handler);

        result.Should().Be(ResultadoDeEntrega.Rechazado);
        handler.Sent.Should().BeEmpty();
    }

    /// <summary>Entrega dos veces: la repetición manda lo mismo con el mismo <c>eventoId</c>, la clave con la que Mensajería no duplica.</summary>
    private static async Task AssertNoticesAsync(IReadOnlyList<AvisoDeEvento> notices, string eventType, BsonDocument data, string expected)
    {
        using var handler = new CapturaDeEntregas(HttpStatusCode.Accepted);
        var targets = expected.Split(',').Select(x => x.Split(':')).ToList();
        var once = targets.Select(x => Body(eventType, x[0], x[1])).ToList();

        (await DeliverAsync(notices, eventType, data, handler)).Should().Be(ResultadoDeEntrega.Aceptado);
        (await DeliverAsync(notices, eventType, data, handler)).Should().Be(ResultadoDeEntrega.Aceptado);

        handler.Sent.Select(x => x.Body).Should().Equal([.. once, .. once]);
        foreach (var sent in handler.Sent)
        {
            var notice = JsonElement.Parse(sent.Body).GetProperty("tipo").GetString()!;
            CapturaDeEntregas.Claims(sent.Token).Should().Contain(("permisos", notice));
        }
    }

    private static string Body(string eventType, string notice, string role)
    {
        var person = role == "cliente" ? _client : _technician;
        return $$"""{"eventoId":"{{eventType}}:{{_visit}}:1","tipo":"{{notice}}","destinatarioId":"{{person}}","rol":"{{role}}","recurso":"{{_visit}}"}""";
    }

    private static Task<ResultadoDeEntrega> DeliverAsync(IReadOnlyList<AvisoDeEvento> notices, string eventType, BsonDocument data, CapturaDeEntregas handler)
    {
        var delivery = new EntregaAvisoDeSolicitud(
            notices.Single(x => x.EventType == eventType), new People(), handler, CapturaDeEntregas.Messaging(), Options.Create(new BandejaOptions()), TimeProvider.System);
        return delivery.DeliverAsync(new MensajeReservado($"{eventType}:{_visit}:1", eventType, data, "lease", 0), CancellationToken.None);
    }

    private static BsonDocument Data(string? proposedBy)
    {
        var data = new BsonDocument { { "visitaId", _visit }, { "solicitudId", "s" }, { "clienteId", _client }, { "tecnicoUsuarioId", _technician } };
        if (proposedBy is not null)
        {
            data.Add("propuestaPor", proposedBy);
        }

        return data;
    }

    private sealed class People : IDestinatariosDeAviso
    {
        public Task<string?> ClientAsync(string requestId, CancellationToken cancellationToken) => Nobody();

        public Task<string?> InvitedTechnicianAsync(string requestId, string technicianId, CancellationToken cancellationToken) => Nobody();

        public Task<string?> QuotingTechnicianAsync(string requestId, string technicianId, CancellationToken cancellationToken) => Nobody();

        public Task<string?> VisitAsync(string requestId, string quoteId, CancellationToken cancellationToken) => Nobody();

        /// <summary>La visita la canceló la persona cliente.</summary>
        public Task<PersonasDeVisita?> VisitPeopleAsync(string visitId, CancellationToken cancellationToken) =>
            Task.FromResult(visitId == _visit ? new PersonasDeVisita(_technician, "cliente") : null);

        private static Task<string?> Nobody() => Task.FromResult<string?>(null);
    }
}
