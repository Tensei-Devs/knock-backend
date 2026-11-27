using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Common.Bandeja;

/// <summary>
/// Entrega <c>visita_afectada</c> (spec 059, R-11; spec 037, R-11) a <c>POST /api/v1/interno/avisos</c> de Mensajería: el aviso al cliente con una cita próxima de un técnico suspendido (token con
/// <c>Services:Messaging:InternalKey</c>, <c>aud</c> <c>knock-notifications</c>, permiso <c>visita_afectada</c>). Viajan solo el destinatario, el rol y la visita: nunca el motivo, el reporte ni quién reportó.
/// El receptor es idempotente por <c>eventoId</c>, así que reintentar es seguro. Sin dirección o sin clave queda para reintento.
/// </summary>
internal sealed class EntregaVisitaAfectada(IHttpClientFactory clients, IConfiguration configuration, IOptions<BandejaOptions> options, TimeProvider clock) : IEntregaDeBandeja
{
    public const string EventType = "visita_afectada";

    public string Tipo => EventType;

    public Task<ResultadoDeEntrega> DeliverAsync(MensajeReservado mensaje, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        var data = mensaje.Datos;
        if (!data.Contains("destinatarioId") || !data.Contains("rol") || !data.Contains("visitaId"))
        {
            return Task.FromResult(ResultadoDeEntrega.Rechazado);
        }

        var body = new { eventoId = mensaje.Id, tipo = EventType, destinatarioId = data["destinatarioId"].AsString, rol = data["rol"].AsString, recurso = data["visitaId"].AsString };
        return EntregaServicio.PostToMessagingAsync(clients, configuration, TimeSpan.FromSeconds(options.Value.TimeoutSeconds), clock, (EntregaOpinionModerada.MessagingPath, EventType), body, cancellationToken);
    }
}
