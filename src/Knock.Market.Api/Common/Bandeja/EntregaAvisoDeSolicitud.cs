using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Common.Bandeja;

/// <summary>
/// Entrega un aviso de solicitudes (spec 037, R-11; spec 054, R-03) a <c>POST /api/v1/interno/avisos</c> de Mensajería, una petición por persona (token con <c>Services:Messaging:InternalKey</c>, <c>aud</c>
/// <c>knock-notifications</c> y permiso del tipo). El <c>eventoId</c> es el del mensaje del buzón y el receptor es idempotente por evento y persona (R-06), así que reintentar es seguro. Se acepta cuando
/// todas las peticiones respondieron 202 o no había a quién avisar; si a algún aviso le faltan datos, se envían los demás y el mensaje queda <c>fallido</c>.
/// </summary>
internal sealed class EntregaAvisoDeSolicitud(
    AvisoDeSolicitud notice,
    IDestinatariosDeAviso recipients,
    IHttpClientFactory clients,
    IConfiguration configuration,
    IOptions<BandejaOptions> options,
    TimeProvider clock) : IEntregaDeBandeja
{
    public string Tipo => notice.EventType;

    public async Task<ResultadoDeEntrega> DeliverAsync(MensajeReservado mensaje, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        var incomplete = false;
        foreach (var target in await notice.ResolveAsync(recipients, mensaje.Datos, cancellationToken))
        {
            if (target is not { Destinatario: { } person, Recurso: { } resource })
            {
                incomplete = true;
                continue;
            }

            var body = new { eventoId = mensaje.Id, tipo = notice.NoticeType, destinatarioId = person, rol = notice.Role, recurso = resource };
            var result = await EntregaServicio.PostToMessagingAsync(clients, configuration, TimeSpan.FromSeconds(options.Value.TimeoutSeconds), clock, (EntregaOpinionModerada.MessagingPath, notice.NoticeType), body, cancellationToken);
            if (result != ResultadoDeEntrega.Aceptado)
            {
                return result;
            }
        }

        return incomplete ? ResultadoDeEntrega.Rechazado : ResultadoDeEntrega.Aceptado;
    }
}
