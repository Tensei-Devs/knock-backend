using System.Net.Http.Headers;
using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Hosting.Procesos.Outbox;
using Microsoft.Extensions.Options;

namespace Knock.Identity.Api.Modules.Account.Services;

/// <summary>Datos de la persona en Mensajería para su exportación (spec 039, R-22): un texto JSON que Identidad guarda como <c>mensajeria.json</c> en el ZIP. Solo mensajes propios.</summary>
internal interface IMessagingExport
{
    Task<string> ExportJsonAsync(string sub, CancellationToken cancellationToken);
}

/// <summary>
/// Cliente de la ruta interna de Mensajería <c>GET /api/v1/interno/cuentas/{sub}/exportacion</c> (spec 039, R-22; spec 054, T-09). Firma un token de servicio de un minuto con el permiso
/// <c>exportacion</c>, <c>aud</c> <c>knock-notifications</c> y el <c>usuarioId</c> de la persona, así que no sirve para otra. Comparte dirección y clave con la entrega de avisos
/// (<c>Services:Messaging</c>, secreto <c>avisos-key-identity</c>). Cualquier fallo —sin dirección o clave, tiempo agotado o error— se lanza como excepción para que la tarea se reintente; no se inventa un resultado.
/// </summary>
internal sealed class MessagingExportClient(HttpClient http, IOptions<EntregaInternaOptions> options, TimeProvider clock) : IMessagingExport
{
    public const string ExportPermission = "exportacion";

    public async Task<string> ExportJsonAsync(string sub, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);
        if (http.BaseAddress is null || ServiceTokenIssuer.ReadKey(options.Value.Messaging.InternalKey) is not { } key)
        {
            throw Unavailable();
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"api/v1/interno/cuentas/{Uri.EscapeDataString(sub)}/exportacion", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            ServiceTokenIssuer.IssueForMessaging(key, ExportPermission, new Dictionary<string, string> { ["usuarioId"] = sub }, clock.GetUtcNow().UtcDateTime));
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(cancellationToken) : throw Unavailable();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            // Mensajería no responde a tiempo o la conexión falló. Si cancela quien llama, la cancelación sigue su camino.
            cancellationToken.ThrowIfCancellationRequested();
            throw Unavailable();
        }
    }

    internal static InvalidOperationException Unavailable() => new("The messaging export route is not available.");
}
