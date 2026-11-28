using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Common.Bandeja;

/// <summary>
/// Entrega <c>ReporteNoTerminadoCreado</c> (spec 050, R-32) a <c>POST /api/v1/interno/reportes-no-terminados</c> de Identidad (spec 053) por HTTP privado con token de servicio HS256 firmado con
/// <c>Security:InternalTokens:MarketKey</c> (<c>iss</c> <c>knock-market</c>, <c>aud</c> <c>knock-identity</c>, permiso <c>reporte-no-terminado-creado</c>, 1 minuto). Viajan solo identificadores; Identidad lee el texto por la
/// ruta interna de Mercado. 202 es entregado; 401, 403 y 422, rechazado; red, 5xx y 429, reintentable. Sin dirección o sin clave el mensaje queda para reintento. Nunca registra la carga ni el token.
/// </summary>
internal sealed class EntregaReporteNoTerminado(IHttpClientFactory clients, IConfiguration configuration, IOptions<BandejaOptions> options, TimeProvider clock) : IEntregaDeBandeja
{
    public const string ClientName = "bandeja-identidad";
    public const string EventType = "ReporteNoTerminadoCreado";
    public const string Path = "api/v1/interno/reportes-no-terminados";
    public const string KeyConfigurationKey = "Security:InternalTokens:MarketKey";
    public const string BaseUrlConfigurationKey = "Services:Identity:BaseUrl";
    public const string Audience = "knock-identity";
    public const string Permission = "reporte-no-terminado-creado";

    public string Tipo => EventType;

    public async Task<ResultadoDeEntrega> DeliverAsync(MensajeReservado mensaje, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        if (EntregaServicio.ReadBaseUri(configuration, BaseUrlConfigurationKey) is not { } baseUri || EntregaServicio.ReadKey(configuration, KeyConfigurationKey) is not { } key)
        {
            return ResultadoDeEntrega.Reintentable;
        }

        if (!mensaje.Datos.Contains("visitaId") || !mensaje.Datos.Contains("clienteId") || !mensaje.Datos.Contains("tecnicoUsuarioId"))
        {
            return ResultadoDeEntrega.Rechazado;
        }

        var body = new
        {
            eventoId = mensaje.Id,
            tipo = EventType,
            visitaId = mensaje.Datos["visitaId"].AsString,
            clienteId = mensaje.Datos["clienteId"].AsString,
            tecnicoUsuarioId = mensaje.Datos["tecnicoUsuarioId"].AsString,
        };
        return await EntregaServicio.PostAsync(clients, ClientName, TimeSpan.FromSeconds(options.Value.TimeoutSeconds), new Uri(baseUri, Path), EntregaServicio.IssueToken(key, Audience, Permission, clock), body, cancellationToken);
    }
}
