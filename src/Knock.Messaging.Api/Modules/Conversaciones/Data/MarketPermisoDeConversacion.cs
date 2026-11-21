using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Knock.Contracts.Conversations;
using Knock.Messaging.Api.Modules.Chat.Options;
using Microsoft.Extensions.Options;

namespace Knock.Messaging.Api.Modules.Conversaciones.Data;

/// <summary>
/// Cliente HTTP tipado de Mercado: <c>GET /api/v1/solicitudes/{id}/conversacion</c> reenviando la sesión de quien pide, con la dirección y el tiempo límite de la spec 026 (spec 030, R-02 y R-09).
/// Un 200 entendible concede; un 400, 401, 403 o 404 niega; cualquier otra cosa (sin respuesta, tiempo agotado, 5xx, cuerpo ilegible) es <see cref="EstadoConsultaPermiso.NoDisponible"/>.
/// Es <c>scoped</c> y no se captura en un singleton. Nunca registra la ruta ni el cuerpo.
/// </summary>
internal sealed class MarketPermisoDeConversacion(HttpClient http) : IPermisoDeConversacion
{
    public async Task<ResultadoPermiso> ConsultarAsync(string solicitudId, string? tecnicoId, string bearerToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solicitudId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bearerToken);
        var path = $"api/v1/solicitudes/{Uri.EscapeDataString(solicitudId)}/conversacion";
        if (!string.IsNullOrEmpty(tecnicoId))
        {
            path += "?tecnicoId=" + Uri.EscapeDataString(tecnicoId);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return response.StatusCode switch
            {
                HttpStatusCode.OK => await ReadAsync(response, cancellationToken),
                HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound => new ResultadoPermiso(EstadoConsultaPermiso.Denegado),
                _ => new ResultadoPermiso(EstadoConsultaPermiso.NoDisponible),
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or OperationCanceledException)
        {
            // Sin respuesta, con tiempo agotado o ilegible: se cierra el paso. Sin registro: el mensaje podría traer la ruta con los identificadores.
            cancellationToken.ThrowIfCancellationRequested();
            return new ResultadoPermiso(EstadoConsultaPermiso.NoDisponible);
        }
    }

    private static async Task<ResultadoPermiso> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var permission = await response.Content.ReadFromJsonAsync<PermisoConversacion>(cancellationToken);
        return permission is null ? new ResultadoPermiso(EstadoConsultaPermiso.NoDisponible) : new ResultadoPermiso(EstadoConsultaPermiso.Concedido, permission);
    }
}

/// <summary>Registro del cliente de Mercado con su dirección y tiempo límite de configuración y sin los registros HTTP por omisión (escribirían la ruta con los identificadores).</summary>
internal static class MarketPermisoRegistration
{
    public static IServiceCollection AddMarketPermisoDeConversacion(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpClient<IPermisoDeConversacion, MarketPermisoDeConversacion>((provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<MarketServiceOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl!, UriKind.Absolute);
                client.Timeout = TimeSpan.FromSeconds(options.TiempoLimiteSegundos);
            })
            .RemoveAllLoggers();
        return services;
    }
}
