using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Knock.Market.Api.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

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
    public const string Issuer = "knock-market";
    public const string Audience = "knock-identity";
    public const string Permission = "reporte-no-terminado-creado";
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(1);

    public string Tipo => EventType;

    public async Task<ResultadoDeEntrega> DeliverAsync(MensajeReservado mensaje, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        if (!Uri.TryCreate(configuration[BaseUrlConfigurationKey], UriKind.Absolute, out var baseUri) || baseUri.Scheme is not ("http" or "https") || ReadKey() is not { } key)
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
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, Path)) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", IssueToken(key));
        try
        {
            using var http = clients.CreateClient(ClientName);
            http.Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
            using var response = await http.SendAsync(request, cancellationToken);
            return response.StatusCode switch
            {
                HttpStatusCode.Accepted => ResultadoDeEntrega.Aceptado,
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.UnprocessableEntity => ResultadoDeEntrega.Rechazado,
                _ => ResultadoDeEntrega.Reintentable,
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Red caída o tiempo agotado: el despachador reprograma el mensaje. Si cancela quien llama, la cancelación sigue su camino.
            return ResultadoDeEntrega.Reintentable;
        }
    }

    private byte[]? ReadKey()
    {
        var configured = configuration[KeyConfigurationKey];
        try
        {
            return string.IsNullOrWhiteSpace(configured) || Convert.FromBase64String(configured) is not { Length: >= InternalServiceAuthentication.MinKeyBytes } key ? null : key;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private string IssueToken(byte[] key)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Claims = new Dictionary<string, object> { ["jti"] = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)), ["permisos"] = new[] { Permission } },
            IssuedAt = now,
            NotBefore = now,
            Expires = now + TokenLifetime,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256),
        });
    }
}
