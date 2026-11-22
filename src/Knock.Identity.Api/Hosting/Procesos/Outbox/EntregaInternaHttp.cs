using System.Net;
using System.Net.Http.Json;
using Knock.Contracts.Safety;
using Knock.Identity.Api.Common.Security;
using Knock.Identity.Api.Modules.HomeAndCar.Services;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace Knock.Identity.Api.Hosting.Procesos.Outbox;

/// <summary>Dirección interna de un servicio destino y la clave HMAC (Base64, 32 bytes o más) con la que Identidad firma su token de servicio. Es un secreto: nunca se registra.</summary>
internal sealed class DestinoInterno
{
    public string? BaseUrl { get; set; }

    public string? InternalKey { get; set; }
}

/// <summary>Sección <c>Services</c> vista por la entrega interna: <c>Market</c> (clave <c>interno-key-identity</c>) y <c>Messaging</c> (clave <c>avisos-key-identity</c>, spec 037, R-11).</summary>
internal sealed class EntregaInternaOptions
{
    public const string SectionName = "Services";

    public DestinoInterno Market { get; set; } = new();

    public DestinoInterno Messaging { get; set; } = new();

    [System.ComponentModel.DataAnnotations.Range(1, 30)]
    public int TimeoutSeconds { get; set; } = 3;
}

/// <summary>
/// Entrega de la bandeja de salida por HTTP privado con token firmado (spec 037, R-11 y R-15). Por ahora entrega los eventos de bloqueo (spec 043, R-23) a <c>POST /api/v1/interno/bloqueos</c> de Mercado y de
/// Mensajería; cualquier otro tipo, destino sin dirección o sin clave queda para reintento, igual que antes de existir esta entrega. 202 es entregado; 401 y 403, rechazado; red, 5xx y 429, reintentable.
/// Nunca registra la carga ni el token.
/// </summary>
internal sealed class EntregaInternaHttp(IHttpClientFactory clients, IOptions<EntregaInternaOptions> options, TimeProvider clock) : IEntregaInterna
{
    public const string ClientName = "entrega-interna";
    public const string Issuer = "knock-identity";
    public const string MarketAudience = "knock-market";
    public const string MessagingAudience = "knock-notifications";
    public const string MarketBlocksPermission = "bloqueos";
    public const string BlocksPath = "api/v1/interno/bloqueos";
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(1);

    public async Task<ResultadoDeEntrega> DeliverAsync(OutboxReservado evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        if (evento.Tipo is not (BlockCreated.EventType or BlockRemoved.EventType) || !TryAim(evento, out var target, out var audience, out var permission))
        {
            return ResultadoDeEntrega.Reintentable;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(target.BaseUri, BlocksPath)) { Content = JsonContent.Create(ToBody(evento)) };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", IssueToken(target.Key, audience, permission));
        try
        {
            using var http = clients.CreateClient(ClientName);
            http.Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
            using var response = await http.SendAsync(request, cancellationToken);
            return response.StatusCode switch
            {
                HttpStatusCode.Accepted => ResultadoDeEntrega.Aceptado,
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ResultadoDeEntrega.Rechazado,
                _ => ResultadoDeEntrega.Reintentable,
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Red caída o tiempo agotado: el procesador reprograma el evento (R-04). Si cancela quien llama, la cancelación sigue su camino.
            return ResultadoDeEntrega.Reintentable;
        }
    }

    private static object ToBody(OutboxReservado evento) => new
    {
        eventoId = evento.Carga.GetValue("eventoId", evento.Id).ToString(),
        tipo = evento.Tipo,
        bloqueaId = evento.Carga.GetValue("bloqueaId", string.Empty).ToString(),
        bloqueadoId = evento.Carga.GetValue("bloqueadoId", string.Empty).ToString(),
        ocurridoEn = evento.Carga.GetValue("ocurridoEn", string.Empty).ToString(),
    };

    /// <summary>El destino del evento con dirección absoluta y clave válida; si falta algo, falso (el evento se reintenta y nada se inventa).</summary>
    private bool TryAim(OutboxReservado evento, out (Uri BaseUri, byte[] Key) target, out string audience, out string[] permission)
    {
        target = default;
        (audience, permission) = evento.Destino switch
        {
            "market" => (MarketAudience, new[] { MarketBlocksPermission }),
            "messaging" => (MessagingAudience, new[] { evento.Tipo }),
            _ => (string.Empty, Array.Empty<string>()),
        };
        var destination = evento.Destino switch { "market" => options.Value.Market, "messaging" => options.Value.Messaging, _ => null };
        if (destination is null || !Uri.TryCreate(destination.BaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme is not ("http" or "https") || MarketHistoryClient.ReadKey(destination.InternalKey) is not { } key)
        {
            return false;
        }

        target = (baseUri, key);
        return true;
    }

    private string IssueToken(byte[] key, string audience, string[] permission) => ServiceTokens.Issue(key, audience, permission, clock.GetUtcNow().UtcDateTime, TokenLifetime);
}
