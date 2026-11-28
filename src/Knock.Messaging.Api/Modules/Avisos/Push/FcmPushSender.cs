using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Knock.Messaging.Api.Modules.Avisos.Push;

/// <summary>Envío real por FCM HTTP v1 (spec 037 R-05): mensaje solo de datos con prioridad alta. Nunca registra el token del dispositivo ni la cuenta de servicio; cualquier error se reporta como <see cref="PushResult"/>.</summary>
internal sealed partial class FcmPushSender(HttpClient http, FcmOptions options, FcmAccessToken accessToken, ILogger<FcmPushSender> logger) : IPushSender
{
    public async Task<PushResult> SendAsync(string deviceToken, IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceToken);
        ArgumentNullException.ThrowIfNull(data);
        try
        {
            var bearer = await accessToken.GetAsync(cancellationToken);
            if (bearer is null)
            {
                TokenUnavailable(logger);
                return PushResult.Failed;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"https://fcm.googleapis.com/v1/projects/{Uri.EscapeDataString(options.ProjectId)}/messages:send"))
            {
                Content = JsonContent.Create(BuildBody(deviceToken, data)),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            using var response = await http.SendAsync(request, cancellationToken);
            return await ClassifyAsync(response, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException or System.Security.Cryptography.CryptographicException && !cancellationToken.IsCancellationRequested)
        {
            SendFailed(logger, ex.GetType().Name);
            return PushResult.Failed;
        }
    }

    internal static object BuildBody(string deviceToken, IReadOnlyDictionary<string, string> data) => new
    {
        message = new
        {
            token = deviceToken,
            data,
            android = new { priority = "HIGH" },
            apns = new { headers = new Dictionary<string, string> { ["apns-priority"] = "10", ["apns-push-type"] = "background" }, payload = new { aps = new { content_available = 1 } } },
        },
    };

    private async Task<PushResult> ClassifyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return PushResult.Sent;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            accessToken.Invalidate();
        }

        var body = response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest
            ? await response.Content.ReadAsStringAsync(cancellationToken)
            : string.Empty;
        if (response.StatusCode == HttpStatusCode.NotFound || body.Contains("UNREGISTERED", StringComparison.Ordinal))
        {
            return PushResult.InvalidToken;
        }

        Rejected(logger, (int)response.StatusCode);
        return PushResult.Failed;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "FCM access token unavailable")]
    private static partial void TokenUnavailable(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "FCM send failed: {ErrorType}")]
    private static partial void SendFailed(ILogger logger, string errorType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "FCM rejected the message with status {Status}")]
    private static partial void Rejected(ILogger logger, int status);
}
