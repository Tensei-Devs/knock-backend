using System.Net.Http.Json;
using System.Text.Json;
using Knock.Contracts.Colonias;
using Knock.Identity.Api.Hosting;

namespace Knock.Identity.Api.Modules.Usuarios.Services;

/// <summary>
/// Cliente del catálogo de colonias de Mercado (spec 017, R-06): <c>GET /api/v1/colonias/existe?nombre=…</c> con la sesión de quien edita su perfil
/// (se reenvía su <c>Authorization</c>; no se crea ninguna credencial nueva). Cualquier fallo —sin URL configurada, sin sesión que reenviar,
/// tiempo agotado, respuesta de error o cuerpo que no se entiende— sale como <see cref="HttpRequestException"/> o <see cref="TimeoutException"/>:
/// el perfil responde 503 y no guarda nada (R-11).
/// </summary>
internal sealed class MarketColoniaCatalog(HttpClient http, IHttpContextAccessor accessor) : IColoniaCatalog
{
    public async Task<bool> ExistsAsync(string colonia, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(colonia);
        if (http.BaseAddress is null)
        {
            throw new HttpRequestException("The colonia catalog URL is not configured.");
        }

        var context = accessor.HttpContext;
        var authorization = context?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authorization))
        {
            throw new HttpRequestException("There is no session to forward to the colonia catalog.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"api/v1/colonias/existe?nombre={Uri.EscapeDataString(colonia)}", UriKind.Relative));
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        if (context is not null && CorrelationId.Current(context) is { } correlationId)
        {
            request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, correlationId);
        }

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ExistsResponse>(cancellationToken);
            return body?.Existe ?? throw new HttpRequestException("The colonia catalog answered without a result.");
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new HttpRequestException("The colonia catalog answered with a body that is not understood.", exception);
        }
    }

    private sealed record ExistsResponse(bool? Existe);
}

/// <summary>Sección <c>Services:Market</c>: dirección interna del servicio de Mercado. Sin <see cref="BaseUrl"/> el catálogo no responde y el perfil con colonia da 503.</summary>
internal sealed class MarketCatalogOptions
{
    public const string SectionName = "Services:Market";

    public string? BaseUrl { get; set; }

    [System.ComponentModel.DataAnnotations.Range(1, 30)]
    public int TimeoutSeconds { get; set; } = 3;

    /// <summary>Vacía (el catálogo no responde) o una dirección http o https sin ruta: <c>http://knock-market</c>; con ruta, la resolución de las rutas relativas dejaría de ser predecible.</summary>
    public bool HasValidBaseUrl() =>
        string.IsNullOrWhiteSpace(BaseUrl)
        || (Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.AbsolutePath.Length == 1 && uri.Query.Length == 0 && uri.Fragment.Length == 0);
}
