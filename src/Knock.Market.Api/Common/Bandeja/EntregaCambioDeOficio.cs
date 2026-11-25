using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Common.Bandeja;

/// <summary>
/// Entrega <c>CambioDeOficio</c> (spec 045, R-20) a <c>POST /api/v1/interno/cambios-de-oficio</c> de Identidad, que la deja en la auditoría del panel (<c>panel_auditoria</c>). Va por HTTP privado con token de servicio
/// HS256 firmado con <c>Security:InternalTokens:MarketKey</c> (<c>iss</c> <c>knock-market</c>, <c>aud</c> <c>knock-identity</c>, permiso <c>cambio-de-oficio</c>, 1 minuto). Viajan solo identificadores (la clave del oficio,
/// la acción y la persona del panel): nunca los valores del oficio. 202 es entregado; 401, 403 y 422, rechazado; red, 5xx y 429, reintentable; el receptor es idempotente por <c>eventoId</c>, así que reintentar es
/// seguro. Sin dirección o sin clave el mensaje queda para reintento. Nunca registra la carga ni el token.
/// </summary>
internal sealed class EntregaCambioDeOficio(IHttpClientFactory clients, IConfiguration configuration, IOptions<BandejaOptions> options, TimeProvider clock) : IEntregaDeBandeja
{
    public const string ClientName = "bandeja-identidad-oficios";
    public const string EventType = "CambioDeOficio";
    public const string Path = "api/v1/interno/cambios-de-oficio";
    public const string Permission = "cambio-de-oficio";

    public string Tipo => EventType;

    public async Task<ResultadoDeEntrega> DeliverAsync(MensajeReservado mensaje, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        var data = mensaje.Datos;
        if (!data.Contains("oficioClave") || !data.Contains("accion") || !data.Contains("porId"))
        {
            return ResultadoDeEntrega.Rechazado;
        }

        if (EntregaServicio.ReadBaseUri(configuration, EntregaReporteNoTerminado.BaseUrlConfigurationKey) is not { } baseUri
            || EntregaServicio.ReadKey(configuration, EntregaReporteNoTerminado.KeyConfigurationKey) is not { } key)
        {
            return ResultadoDeEntrega.Reintentable;
        }

        var body = new { eventoId = mensaje.Id, tipo = EventType, oficioClave = data["oficioClave"].AsString, accion = data["accion"].AsString, porId = data["porId"].AsString };
        return await EntregaServicio.PostAsync(
            clients,
            ClientName,
            TimeSpan.FromSeconds(options.Value.TimeoutSeconds),
            new Uri(baseUri, Path),
            EntregaServicio.IssueToken(key, EntregaReporteNoTerminado.Audience, Permission, clock),
            body,
            cancellationToken);
    }
}
