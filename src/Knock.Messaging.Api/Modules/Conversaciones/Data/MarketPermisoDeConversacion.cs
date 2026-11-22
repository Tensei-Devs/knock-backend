using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Knock.Contracts.Conversations;
using Knock.Messaging.Api.Modules.Chat.Options;
using Microsoft.Extensions.Options;

namespace Knock.Messaging.Api.Modules.Conversaciones.Data;

/// <summary>
/// Cliente HTTP tipado de Mercado: <c>GET /api/v1/solicitudes/{id}/conversacion</c> reenviando la sesión de quien pide, con la dirección y el tiempo límite de la spec 026 (spec 030, R-02 y R-09).
/// Un 200 entendible concede (y, con <c>conversacionId</c>, guarda el permiso 60 s); un 400, 401, 403 o 404 es <see cref="EstadoPermisoConversacion.NoEncontrado"/>; cualquier otra cosa (sin
/// respuesta, tiempo agotado, 5xx, cuerpo ilegible) responde con el permiso en caché si lo hay y, si no, <see cref="EstadoPermisoConversacion.NoDisponible"/>. Con el permiso en caché los datos que
/// solo conoce Mercado (participantes, servicio, colonia) vienen vacíos: solo valen <c>Abierta</c>, <c>MotivoCierre</c>, <c>Elegido</c> y <c>VisitaId</c>. Nunca registra la ruta ni el cuerpo.
/// </summary>
internal sealed class MarketPermisoDeConversacion(HttpClient http, PermisoCache cache) : IPermisoDeConversacion
{
    public async Task<ResultadoPermisoConversacion> ConsultarAsync(
        string solicitudId,
        string? tecnicoId,
        string? conversacionId,
        string subject,
        string bearerToken,
        CancellationToken cancellationToken)
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
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            {
                return new ResultadoPermisoConversacion(EstadoPermisoConversacion.NoEncontrado);
            }

            if (response.StatusCode == HttpStatusCode.OK
                && await response.Content.ReadFromJsonAsync<PermisoConversacion>(cancellationToken) is { } permission)
            {
                if (conversacionId is not null)
                {
                    await cache.SetAsync(conversacionId, new PermisoVigente(permission.Abierta, permission.MotivoCierre, permission.Elegido, permission.VisitaId));
                }

                return new ResultadoPermisoConversacion(EstadoPermisoConversacion.Concedido, permission);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or OperationCanceledException)
        {
            // Sin respuesta, con tiempo agotado o ilegible: se usa la caché o se cierra el paso. Sin registro: el mensaje podría traer la ruta con los identificadores.
            cancellationToken.ThrowIfCancellationRequested();
        }

        return await FromCacheAsync(solicitudId, tecnicoId, conversacionId);
    }

    private async Task<ResultadoPermisoConversacion> FromCacheAsync(string solicitudId, string? tecnicoId, string? conversacionId)
    {
        if (conversacionId is null || await cache.GetAsync(conversacionId) is not { } cached)
        {
            return new ResultadoPermisoConversacion(EstadoPermisoConversacion.NoDisponible);
        }

        var permission = new PermisoConversacion(
            solicitudId,
            tecnicoId ?? string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            cached.Abierta,
            cached.MotivoCierre,
            cached.Elegido,
            cached.VisitaId,
            string.Empty,
            string.Empty,
            new TecnicoPublicoConversacion(string.Empty, null));
        return new ResultadoPermisoConversacion(EstadoPermisoConversacion.Concedido, permission);
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
