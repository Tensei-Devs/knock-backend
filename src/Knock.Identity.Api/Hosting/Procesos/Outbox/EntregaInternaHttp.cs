using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Knock.Contracts.Safety;
using Knock.Identity.Api.Modules.HomeAndCar.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
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
/// Entrega de la bandeja de salida por HTTP privado con token firmado (spec 037, R-11 y R-15). Entrega los eventos de bloqueo (spec 043, R-23) a <c>POST /api/v1/interno/bloqueos</c> de Mercado y de Mensajería,
/// <c>ReporteDecidido</c> (spec 053, avisos de texto fijo de la 037) a <c>POST /api/v1/interno/avisos</c> de Mensajería con solo la persona y el reporte, la suspensión y reactivación (spec 059) a <c>POST /api/v1/interno/eventos</c> de Mercado, <c>CuentaEliminada</c> (spec 039) a <c>POST /api/v1/interno/cuentas/{usuarioId}/eliminada</c> de Mercado y
/// <c>sesion_revocada</c> (spec 054) a <c>POST /api/v1/interno/avisos</c> de Mensajería; cualquier otro tipo, destino sin dirección o sin clave queda para reintento, igual que antes de existir esta entrega.
/// 202 es entregado; 401 y 403, rechazado; red, 5xx y 429, reintentable. Nunca registra la carga ni el token.
/// </summary>
internal sealed class EntregaInternaHttp(IHttpClientFactory clients, IOptions<EntregaInternaOptions> options, TimeProvider clock) : IEntregaInterna
{
    public const string ClientName = "entrega-interna";
    public const string Issuer = "knock-identity";
    public const string MarketAudience = "knock-market";
    public const string MessagingAudience = "knock-notifications";
    public const string MarketBlocksPermission = "bloqueos";
    public const string MarketEventsPermission = "eventos";
    public const string MarketAccountPermission = "cuenta";
    public const string ReportDecidedNotice = "reporte_decidido";
    public const string BlocksPath = "api/v1/interno/bloqueos";
    public const string EventsPath = "api/v1/interno/eventos";
    public const string NoticesPath = "api/v1/interno/avisos";
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(1);

    public async Task<ResultadoDeEntrega> DeliverAsync(OutboxReservado evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        if (evento.Tipo == "ApelacionResuelta")
        {
            // Solo para auditoría (spec 060, R-10): no tiene servicio destino ni push; queda en la bandeja como entregado.
            return ResultadoDeEntrega.Aceptado;
        }

        if (Plan(evento) is not { } plan || !TryAim(evento.Destino, out var target))
        {
            return ResultadoDeEntrega.Reintentable;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(target.BaseUri, plan.Path)) { Content = JsonContent.Create(plan.Body) };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", IssueToken(target.Key, plan));
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

    /// <summary>Lo que se envía de un evento: ruta, cuerpo, audiencia, permisos del token y claims que lo atan a la persona. <see langword="null"/> si el tipo o el destino no tienen entrega o falta un dato (se reintenta).</summary>
    private static EntregaPlan? Plan(OutboxReservado evento) => (evento.Tipo, evento.Destino) switch
    {
        (BlockCreated.EventType or BlockRemoved.EventType, "market") => new EntregaPlan(BlocksPath, BlockBody(evento), MarketAudience, [MarketBlocksPermission], null),
        (BlockCreated.EventType or BlockRemoved.EventType, "messaging") => new EntregaPlan(BlocksPath, BlockBody(evento), MessagingAudience, [evento.Tipo], null),
        ("CuentaSuspendida" or "CuentaReactivada", "market") when Text(evento, "usuarioId") is { } user => new EntregaPlan(
            EventsPath,
            new { eventoId = Text(evento, "eventoId") ?? evento.Id, tipo = evento.Tipo, usuarioId = user, reporteId = Text(evento, "reporteId"), desde = Text(evento, "desde") },
            MarketAudience,
            [MarketEventsPermission],
            null),
        ("CuentaEliminada", "market") when Text(evento, "sub") is { } sub => new EntregaPlan(
            $"api/v1/interno/cuentas/{Uri.EscapeDataString(sub)}/eliminada",
            new { },
            MarketAudience,
            [MarketAccountPermission],
            new Dictionary<string, object> { ["usuarioId"] = sub }),
        ("ReporteDecidido", "messaging") when Text(evento, "usuarioId") is { } person && Text(evento, "reporteId") is { } report => new EntregaPlan(
            NoticesPath,
            new { eventoId = evento.Id, tipo = ReportDecidedNotice, destinatarioId = person, recurso = report },
            MessagingAudience,
            [ReportDecidedNotice],
            null),
        ("sesion_revocada", "messaging") when Text(evento, "sid") is { } sid => new EntregaPlan(NoticesPath, new { eventoId = evento.Id, tipo = evento.Tipo, sid }, MessagingAudience, [evento.Tipo], null),
        _ => null,
    };

    private static string? Text(OutboxReservado evento, string field) => evento.Carga.TryGetValue(field, out var value) && value.IsString && value.AsString.Length > 0 ? value.AsString : null;

    private static object BlockBody(OutboxReservado evento) => new
    {
        eventoId = evento.Carga.GetValue("eventoId", evento.Id).ToString(),
        tipo = evento.Tipo,
        bloqueaId = evento.Carga.GetValue("bloqueaId", string.Empty).ToString(),
        bloqueadoId = evento.Carga.GetValue("bloqueadoId", string.Empty).ToString(),
        ocurridoEn = evento.Carga.GetValue("ocurridoEn", string.Empty).ToString(),
    };

    /// <summary>El destino con dirección absoluta y clave válida; si falta algo, falso (el evento se reintenta y nada se inventa).</summary>
    private bool TryAim(string destination, out (Uri BaseUri, byte[] Key) target)
    {
        target = default;
        var configured = destination switch { "market" => options.Value.Market, "messaging" => options.Value.Messaging, _ => null };
        if (configured is null || !Uri.TryCreate(configured.BaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme is not ("http" or "https") || MarketHistoryClient.ReadKey(configured.InternalKey) is not { } key)
        {
            return false;
        }

        target = (baseUri, key);
        return true;
    }

    private string IssueToken(byte[] key, EntregaPlan plan)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var claims = new Dictionary<string, object>(plan.Claims ?? new Dictionary<string, object>()) { ["jti"] = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)), ["permisos"] = plan.Permission };
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = plan.Audience,
            Claims = claims,
            IssuedAt = now,
            NotBefore = now,
            Expires = now + TokenLifetime,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256),
        });
    }

    private sealed record EntregaPlan(string Path, object Body, string Audience, string[] Permission, IReadOnlyDictionary<string, object>? Claims);
}
