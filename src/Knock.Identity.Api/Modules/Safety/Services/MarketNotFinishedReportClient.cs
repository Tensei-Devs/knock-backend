using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Modules.HomeAndCar.Services;
using Knock.Identity.Api.Modules.Safety.Dtos;
using Microsoft.Extensions.Options;

namespace Knock.Identity.Api.Modules.Safety.Services;

/// <summary>Cómo respondió Mercado al pedir el reporte de «no terminado».</summary>
internal enum MarketReportStatus
{
    Found,
    NotFound,
    Unavailable,
}

/// <summary>Resultado de pedir el reporte a Mercado: el reporte, o por qué no hay.</summary>
internal sealed record MarketReportResult(MarketReportStatus Status, MarketNotFinishedReport? Report)
{
    public static MarketReportResult Failure(MarketReportStatus status) => new(status, null);

    public sealed override string ToString() => nameof(MarketReportResult);
}

/// <summary>Lo que el consumidor del reporte de «no terminado» necesita de Mercado (spec 053); existe para probarlo con dobles.</summary>
internal interface IMarketNotFinishedReports
{
    /// <summary>El reporte de «no terminado» de la visita. Nunca lanza por un fallo de Mercado: lo devuelve en el resultado.</summary>
    Task<MarketReportResult> ReadAsync(string visitId, CancellationToken cancellationToken);
}

/// <summary>
/// Cliente de la ruta interna de Mercado <c>GET /api/v1/interno/visitas/{visitaId}/reporte-no-terminado</c> (spec 053). Firma un token de servicio de un minuto con el permiso <c>reporte-no-terminado</c> y el
/// <c>visitaId</c> pedido, igual que el historial (<see cref="MarketHistoryClient"/>, misma dirección y clave <c>Services:Market</c>). Sin dirección o sin clave, tiempo agotado, error o cuerpo ilegible salen como
/// <see cref="MarketReportStatus.Unavailable"/>: no se inventa un reporte. Nunca registra el texto ni el token.
/// </summary>
internal sealed class MarketNotFinishedReportClient(HttpClient http, IOptions<MarketHistoryOptions> options, TimeProvider clock) : IMarketNotFinishedReports
{
    public const string Permission = "reporte-no-terminado";

    public async Task<MarketReportResult> ReadAsync(string visitId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(visitId);
        if (http.BaseAddress is null || MarketHistoryClient.ReadKey(options.Value.InternalKey) is not { } key)
        {
            return MarketReportResult.Failure(MarketReportStatus.Unavailable);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"api/v1/interno/visitas/{Uri.EscapeDataString(visitId)}/reporte-no-terminado", UriKind.Relative));
        var token = ServiceTokenIssuer.IssueForMarket(key, Permission, new Dictionary<string, string> { ["visitaId"] = visitId }, clock.GetUtcNow().UtcDateTime);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return MarketReportResult.Failure(MarketReportStatus.NotFound);
            }

            if (!response.IsSuccessStatusCode)
            {
                return MarketReportResult.Failure(MarketReportStatus.Unavailable);
            }

            var body = await response.Content.ReadFromJsonAsync<MarketNotFinishedReport>(cancellationToken);
            return body is { VisitaId.Length: > 0, ClienteId.Length: > 0, TecnicoUsuarioId.Length: > 0, Texto.Length: > 0 }
                ? new MarketReportResult(MarketReportStatus.Found, body)
                : MarketReportResult.Failure(MarketReportStatus.Unavailable);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException or InvalidOperationException)
        {
            // Mercado no responde a tiempo o el cuerpo no se entiende: el evento se reintenta. Si cancela quien llama, la cancelación sigue su camino.
            cancellationToken.ThrowIfCancellationRequested();
            return MarketReportResult.Failure(MarketReportStatus.Unavailable);
        }
    }
}
