using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Knock.Market.Api.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Knock.Market.Api.Common.Bandeja;

/// <summary>
/// Pieza común de las entregas de la bandeja de Mercado por HTTP privado: lee la dirección y la clave HMAC de la configuración, emite el token de servicio HS256 de 1 minuto (<c>iss</c> <c>knock-market</c>,
/// <c>jti</c> y <c>permisos</c>) y traduce la respuesta: 202 es entregado; 401, 403 y 422, rechazado; red, 5xx y 429, reintentable. Nunca registra la carga ni el token.
/// </summary>
internal static class EntregaServicio
{
    public const string Issuer = "knock-market";
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(1);

    /// <summary>La dirección absoluta http o https de la configuración, o <see langword="null"/>.</summary>
    public static Uri? ReadBaseUri(IConfiguration configuration, string key) =>
        Uri.TryCreate(configuration[key], UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri : null;

    /// <summary>La clave de la configuración si es Base64 de al menos 32 bytes; si no, <see langword="null"/>.</summary>
    public static byte[]? ReadKey(IConfiguration configuration, string key)
    {
        var configured = configuration[key];
        try
        {
            return string.IsNullOrWhiteSpace(configured) || Convert.FromBase64String(configured) is not { Length: >= InternalServiceAuthentication.MinKeyBytes } bytes ? null : bytes;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static string IssueToken(byte[] key, string audience, string permission, TimeProvider clock)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            Claims = new Dictionary<string, object> { ["jti"] = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)), ["permisos"] = new[] { permission } },
            IssuedAt = now,
            NotBefore = now,
            Expires = now + TokenLifetime,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256),
        });
    }

    public static async Task<ResultadoDeEntrega> PostAsync(IHttpClientFactory clients, string clientName, TimeSpan timeout, Uri uri, string token, object body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var http = clients.CreateClient(clientName);
            http.Timeout = timeout;
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
}
