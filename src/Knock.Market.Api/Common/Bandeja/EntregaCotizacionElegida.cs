using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Common.Bandeja;

/// <summary>
/// Entrega <c>CotizacionElegida</c> (spec 048, R-14; spec 054, R-03) a <c>POST /api/v1/interno/eventos</c> de Mensajería, que cierra las demás conversaciones de la solicitud (token con
/// <c>Services:Messaging:InternalKey</c>, <c>aud</c> <c>knock-notifications</c>, permiso <c>CotizacionElegida</c>). Viajan solo identificadores y el cuerpo es el cerrado del receptor. El receptor es idempotente, así
/// que reintentar es seguro. Sin dirección o sin clave queda para reintento.
/// </summary>
internal sealed class EntregaCotizacionElegida(IHttpClientFactory clients, IConfiguration configuration, IOptions<BandejaOptions> options, TimeProvider clock) : IEntregaDeBandeja
{
    public const string EventType = "CotizacionElegida";
    public const string Path = "api/v1/interno/eventos";

    public string Tipo => EventType;

    public Task<ResultadoDeEntrega> DeliverAsync(MensajeReservado mensaje, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        var data = mensaje.Datos;
        if (!data.Contains("solicitudId") || !data.Contains("tecnicoUsuarioId"))
        {
            return Task.FromResult(ResultadoDeEntrega.Rechazado);
        }

        var body = new { eventoId = mensaje.Id, tipo = EventType, solicitudId = data["solicitudId"].AsString, tecnicoUsuarioId = data["tecnicoUsuarioId"].AsString };
        return EntregaServicio.PostToMessagingAsync(clients, configuration, TimeSpan.FromSeconds(options.Value.TimeoutSeconds), clock, (Path, EventType), body, cancellationToken);
    }
}
