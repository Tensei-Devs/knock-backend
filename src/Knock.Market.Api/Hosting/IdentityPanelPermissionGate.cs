using System.Net.Http.Json;
using Knock.Market.Api.Common.Bandeja;

namespace Knock.Market.Api.Hosting;

/// <summary>
/// Puerta real de permisos del panel en Mercado (spec 061, sobre la 033 R-21 y la 056): pregunta a Identidad, dueña de las cuentas, con <c>POST /api/v1/interno/permisos-del-panel</c> y el token de servicio
/// de Mercado (<c>Security:InternalTokens:MarketKey</c>, <c>aud</c> <c>knock-identity</c>, permiso <c>panel-permiso</c>). Identidad decide con los roles vigentes y la versión de permisos; el doble factor
/// ya se cumplió porque Identidad solo emite el token del panel tras el TOTP. Falla cerrado: sin dirección o sin clave, red caída, tiempo agotado, cualquier respuesta distinta de 200 o cuerpo ilegible
/// es <see cref="PanelDecision.Denied"/>. Nunca registra ni reenvía el token del panel.
/// </summary>
internal sealed class IdentityPanelPermissionGate(IHttpClientFactory clients, IConfiguration configuration, TimeProvider clock) : IPanelPermissionGate
{
    public const string ClientName = "panel-permisos-identidad";
    public const string Path = "api/v1/interno/permisos-del-panel";
    public const string Permission = "panel-permiso";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public async Task<PanelDecision> DecideAsync(string subject, string sessionId, string permissionsVersion, string permission, CancellationToken cancellationToken)
    {
        if (!int.TryParse(permissionsVersion, out var version)
            || EntregaServicio.ReadBaseUri(configuration, EntregaOpinionModerada.IdentityBaseUrlConfigurationKey) is not { } baseUri
            || EntregaServicio.ReadKey(configuration, EntregaOpinionModerada.IdentityKeyConfigurationKey) is not { } key)
        {
            return PanelDecision.Denied;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, Path)) { Content = JsonContent.Create(new { sub = subject, sid = sessionId, pv = version, permiso = permission }) };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", EntregaServicio.IssueToken(key, EntregaOpinionModerada.IdentityAudience, Permission, clock));
        try
        {
            using var http = clients.CreateClient(ClientName);
            http.Timeout = Timeout;
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return PanelDecision.Denied;
            }

            var body = await response.Content.ReadFromJsonAsync<Answer>(cancellationToken);
            return body?.Permitido == true ? PanelDecision.Allowed : PanelDecision.Denied;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or NotSupportedException && !cancellationToken.IsCancellationRequested)
        {
            return PanelDecision.Denied;
        }
    }

    private sealed record Answer(bool Permitido);
}
