using System.Net.Http.Json;
using System.Text.Json;

namespace Knock.IntegrationTest.Support;

/// <summary>Llamadas HTTP de las pruebas al canje de la spec 015.</summary>
internal static class AuthHttp
{
    public static readonly Uri ExchangeUri = new("/api/v1/auth/firebase", UriKind.Relative);

    /// <summary>Canje válido en su forma: con <c>deviceId</c> (UUID), <c>aceptaAviso: true</c> y nombre del dispositivo.</summary>
    public static Task<HttpResponseMessage> ExchangeAsync(HttpClient client, string firebaseIdToken, string? deviceId = null, string? remoteIp = null) =>
        SendAsync(
            client,
            new { firebaseIdToken, deviceId = deviceId ?? Guid.NewGuid().ToString("D"), aceptaAviso = true, dispositivo = "Teléfono de prueba" },
            remoteIp);

    /// <summary>Canje con el cuerpo que se indique, para los casos de forma inválida.</summary>
    public static async Task<HttpResponseMessage> SendAsync(HttpClient client, object body, string? remoteIp = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, ExchangeUri) { Content = JsonContent.Create(body) };
        if (remoteIp is not null)
        {
            request.Headers.Add(IdentityApiFactory.RemoteIpHeader, remoteIp);
        }

        return await client.SendAsync(request);
    }

    public static async Task<(string AccessToken, string RefreshToken)> ReadSessionAsync(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (body.RootElement.GetProperty("accessToken").GetString()!, body.RootElement.GetProperty("refreshToken").GetString()!);
    }
}
