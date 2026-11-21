using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Common.Moderation;

/// <summary>Severidad por categoría que devolvió Azure (0, 2, 4 o 6 con la salida de cuatro niveles).</summary>
internal sealed record SafetyAnalysis(IReadOnlyDictionary<string, int> Severities);

/// <summary>Único punto (devuelve <c>null</c> cuando Azure no dio una respuesta válida: tiempo agotado, 5xx o 429 tras el reintento, 401 o 403, cuerpo ilegible, sin credencial o cortacircuito abierto). que conoce el formato de Azure AI Content Safety (spec 031, R-05, R-07, R-08, R-09, R-16).</summary>
internal interface IContentSafetyClient
{
    Task<SafetyAnalysis?> AnalyzeTextAsync(string text, CancellationToken cancellationToken);

    Task<SafetyAnalysis?> AnalyzeImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken);
}

/// <summary>
/// Cliente HTTP tipado de Content Safety por REST (sin el SDK, decisión 3 de la spec). Tiempo máximo por llamada, un reintento ante 429, 5xx o error de red (respetando <c>Retry-After</c> de 2 s o
/// menos; sin reintento ante 401, 403 o 4xx) y cortacircuito por réplica: se abre 30 s tras 5 fallos seguidos. La credencial es la identidad administrada (<see cref="TokenCredential"/> registrada por
/// el despliegue) o, si existe, la clave que Key Vault entrega por configuración.
/// </summary>
internal sealed class ContentSafetyClient : IContentSafetyClient
{
    internal const int FailuresToOpen = 5;
    internal static readonly TimeSpan OpenDuration = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(2);
    internal static readonly string[] Categories = ["Hate", "Sexual", "Violence", "SelfHarm"];
    private const string _scope = "https://cognitiveservices.azure.com/.default";

    private readonly HttpClient _http;
    private readonly ModerationOptions _options;
    private readonly TimeProvider _clock;
    private readonly TokenCredential? _credential;
    private readonly Lock _gate = new();
    private int _consecutiveFailures;
    private DateTimeOffset _openUntil = DateTimeOffset.MinValue;

    public ContentSafetyClient(HttpClient http, IOptions<ModerationOptions> options, TimeProvider clock, TokenCredential? credential = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        _http = http;
        _options = options.Value;
        _clock = clock;
        _credential = credential;
    }

    public Task<SafetyAnalysis?> AnalyzeTextAsync(string text, CancellationToken cancellationToken) =>
        CallAsync("text:analyze", new { text, categories = Categories, outputType = "FourSeverityLevels" }, cancellationToken);

    public Task<SafetyAnalysis?> AnalyzeImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) =>
        CallAsync("image:analyze", new { image = new { content = Convert.ToBase64String(image.Span) }, categories = Categories, outputType = "FourSeverityLevels" }, cancellationToken);

    private async Task<SafetyAnalysis?> CallAsync(string operation, object body, CancellationToken cancellationToken)
    {
        if (IsOpen())
        {
            return null;
        }

        var analysis = await SendWithRetryAsync(operation, body, cancellationToken);
        if (analysis is null)
        {
            RegisterFailure();
            return null;
        }

        lock (_gate)
        {
            _consecutiveFailures = 0;
        }

        return analysis;
    }

    private bool IsOpen()
    {
        lock (_gate)
        {
            return _clock.GetUtcNow() < _openUntil;
        }
    }

    private void RegisterFailure()
    {
        lock (_gate)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= FailuresToOpen)
            {
                _openUntil = _clock.GetUtcNow() + OpenDuration;
                _consecutiveFailures = 0;
            }
        }
    }

    private async Task<SafetyAnalysis?> SendWithRetryAsync(string operation, object body, CancellationToken cancellationToken)
    {
        var result = await SendOnceAsync(operation, body, cancellationToken);
        if (result.Analysis is not null || !result.Retryable || result.RetryAfter > MaxRetryAfter)
        {
            return result.Analysis;
        }

        if (result.RetryAfter > TimeSpan.Zero)
        {
            await Task.Delay(result.RetryAfter, _clock, cancellationToken);
        }

        return (await SendOnceAsync(operation, body, cancellationToken)).Analysis;
    }

    private async Task<AttemptResult> SendOnceAsync(string operation, object body, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TiempoMaximoSegundos));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, string.Create(CultureInfo.InvariantCulture, $"/contentsafety/{operation}?api-version={_options.VersionApi}"))
            {
                Content = JsonContent.Create(body),
            };
            if (!await AuthorizeAsync(request, timeout.Token))
            {
                return new AttemptResult(null, TimeSpan.Zero, false);
            }

            using var response = await _http.SendAsync(request, timeout.Token);
            if (response.IsSuccessStatusCode)
            {
                return new AttemptResult(await ParseAsync(response, timeout.Token), TimeSpan.Zero, false);
            }

            var transient = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
            return new AttemptResult(null, RetryAfterOf(response.Headers.RetryAfter), transient);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Tiempo agotado o error de red: transitorio. Solo se conserva el tipo, nunca el mensaje.
            return new AttemptResult(null, TimeSpan.Zero, true);
        }
    }

    /// <summary>Falso si no hay ni clave ni credencial: falla cerrado sin llamar a la red.</summary>
    private async Task<bool> AuthorizeAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_options.ContentSafety.Clave))
        {
            request.Headers.Add("Ocp-Apim-Subscription-Key", _options.ContentSafety.Clave);
            return true;
        }
        else if (_credential is not null)
        {
            var token = await _credential.GetTokenAsync(new TokenRequestContext([_scope]), cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            return true;
        }

        return false;
    }

    private static TimeSpan RetryAfterOf(RetryConditionHeaderValue? header) =>
        header?.Delta ?? (header?.Date is { } date ? date - DateTimeOffset.UtcNow : TimeSpan.Zero);

    private static async Task<SafetyAnalysis?> ParseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var severities = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in document.RootElement.GetProperty("categoriesAnalysis").EnumerateArray())
            {
                severities[item.GetProperty("category").GetString() ?? string.Empty] = item.GetProperty("severity").GetInt32();
            }

            return Categories.All(severities.ContainsKey) ? new SafetyAnalysis(severities) : null;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private sealed record AttemptResult(SafetyAnalysis? Analysis, TimeSpan RetryAfter, bool Retryable);
}
