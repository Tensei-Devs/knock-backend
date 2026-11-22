using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Modules.Account.Domain;
using Knock.Identity.Api.Modules.HomeAndCar.Services;
using Microsoft.Extensions.Options;

namespace Knock.Identity.Api.Modules.Account.Services;

/// <summary>
/// Cliente de las rutas internas de la cuenta en Mercado (<c>/api/v1/interno/cuentas/{sub}/...</c>; spec 039, R-22, R-24 y R-25). Firma un token de servicio de un minuto con el permiso <c>cuenta</c> y el
/// <c>usuarioId</c> de la persona, así que no sirve para otra. Comparte dirección y clave con el historial (<c>Services:Market</c>). Cualquier fallo —sin dirección o clave, tiempo agotado, error o cuerpo ilegible—
/// sale como 503 <c>market_unavailable</c> (<see cref="UnavailableMarketAccountClient"/>): no se inventa un resultado.
/// </summary>
internal sealed class MarketAccountClient(HttpClient http, IOptions<MarketHistoryOptions> options, TimeProvider clock) : IMarketAccountClient, IMarketExport
{
    public const string AccountPermission = "cuenta";

    public async Task<AccountPending> GetPendingAsync(string sub, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, sub, "pendientes", cancellationToken);
        var body = await ReadAsync<MarketPending>(response, cancellationToken);
        return body is { AppointmentIds: not null, QuoteIds: not null }
            ? new AccountPending(body.AppointmentTotal, body.AppointmentIds, body.QuoteTotal, body.QuoteIds, body.VisitInProgress)
            : throw UnavailableMarketAccountClient.Unavailable();
    }

    public async Task CancelOpenAsync(string sub, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, sub, "cancelaciones", cancellationToken);
    }

    public async Task<string> ExportJsonAsync(string sub, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, sub, "exportacion", cancellationToken);
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            throw UnavailableMarketAccountClient.Unavailable();
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string sub, string action, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);
        if (http.BaseAddress is null || ServiceTokenIssuer.ReadKey(options.Value.InternalKey) is not { } key)
        {
            throw UnavailableMarketAccountClient.Unavailable();
        }

        using var request = new HttpRequestMessage(method, new Uri($"api/v1/interno/cuentas/{Uri.EscapeDataString(sub)}/{action}", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ServiceTokenIssuer.IssueForMarket(key, AccountPermission, new Dictionary<string, string> { ["usuarioId"] = sub }, clock.GetUtcNow().UtcDateTime));
        try
        {
            var response = await http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            response.Dispose();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            // Mercado no responde a tiempo o la conexión falló. Si cancela quien llama, la cancelación sigue su camino.
            cancellationToken.ThrowIfCancellationRequested();
        }

        throw UnavailableMarketAccountClient.Unavailable();
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw UnavailableMarketAccountClient.Unavailable();
        }
    }

    private sealed record MarketPending(int AppointmentTotal, IReadOnlyList<string>? AppointmentIds, int QuoteTotal, IReadOnlyList<string>? QuoteIds, bool VisitInProgress);
}
