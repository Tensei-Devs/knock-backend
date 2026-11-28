using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Modules.Assistant.Services;

/// <summary>Respuesta del proveedor: <c>Json</c> es null cuando no hubo respuesta válida (tiempo, 5xx, 429 tras el reintento, 401 o 403, cuerpo ilegible o cortacircuito abierto).</summary>
internal sealed record ProviderResponse(string? Json, int InputTokens, int OutputTokens);

/// <summary>Envuelve el cliente de Azure OpenAI y expone solo «pide esto y devuelve el JSON» (spec 024, §7). Las imágenes van dentro de la petición; ningún enlace firmado sale.</summary>
internal interface IProveedorIa
{
    Task<ProviderResponse> CompleteAsync(string systemPrompt, string censoredText, IReadOnlyList<byte[]> images, int maxOutputTokens, CancellationToken cancellationToken);
}

/// <summary>Fuente de las fotos de una solicitud: devuelve la versión reducida y sin metadatos solo si el archivo es <c>uso = solicitud</c>, está listo y es de la misma persona (spec 024, R-04).</summary>
internal interface IAiImageSource
{
    Task<byte[]?> GetReducedImageAsync(Guid fileId, string ownerId, CancellationToken cancellationToken);
}

/// <summary>Mientras <c>IMediaApi</c> no exponga el uso y la versión reducida (spec 020, T-01) toda imagen se rechaza y se responde con la plantilla.</summary>
internal sealed class UnavailableAiImageSource : IAiImageSource
{
    public Task<byte[]?> GetReducedImageAsync(Guid fileId, string ownerId, CancellationToken cancellationToken) => Task.FromResult<byte[]?>(null);
}

/// <summary>
/// Cliente REST de Azure OpenAI (decisión: sin el SDK, alternativa que la spec evalúa). Tiempo máximo por llamada, un reintento ante 429, 5xx o error de red (nunca ante 400, 401 o 403) y
/// cortacircuito por réplica: se abre 60 s tras 5 fallos seguidos. Sin clave ni credencial no hay llamada.
/// </summary>
internal sealed class ProveedorIa(HttpClient http, IOptions<AssistantOptions> options, TimeProvider clock, TokenCredential? credential = null) : IProveedorIa
{
    internal const int FailuresToOpen = 5;
    internal static readonly TimeSpan OpenDuration = TimeSpan.FromSeconds(60);
    private const string _scope = "https://cognitiveservices.azure.com/.default";
    private static readonly ProviderResponse _failed = new(null, 0, 0);

    private readonly AssistantOptions _options = options.Value;
    private readonly Lock _gate = new();
    private int _failures;
    private DateTimeOffset _openUntil = DateTimeOffset.MinValue;

    public async Task<ProviderResponse> CompleteAsync(string systemPrompt, string censoredText, IReadOnlyList<byte[]> images, int maxOutputTokens, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (clock.GetUtcNow() < _openUntil)
            {
                return _failed;
            }
        }

        var body = BuildBody(systemPrompt, censoredText, images, maxOutputTokens);
        var response = await SendAsync(body, cancellationToken);
        if (response.Retry)
        {
            response = await SendAsync(body, cancellationToken);
        }

        lock (_gate)
        {
            if (response.Result.Json is not null)
            {
                _failures = 0;
            }
            else if (++_failures >= FailuresToOpen)
            {
                _openUntil = clock.GetUtcNow() + OpenDuration;
                _failures = 0;
            }
        }

        return response.Result;
    }

    private static object BuildBody(string systemPrompt, string censoredText, IReadOnlyList<byte[]> images, int maxOutputTokens)
    {
        var parts = new List<object> { new { type = "text", text = censoredText } };
        parts.AddRange(images.Select(image => (object)new { type = "image_url", image_url = new { url = "data:image/jpeg;base64," + Convert.ToBase64String(image) } }));
        return new
        {
            messages = new object[] { new { role = "system", content = systemPrompt }, new { role = "user", content = parts } },
            max_completion_tokens = maxOutputTokens,
            response_format = new { type = "json_object" },
        };
    }

    private async Task<(ProviderResponse Result, bool Retry)> SendAsync(object body, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.Limites.TiempoMaximoSegundos));
        try
        {
            var section = _options.AzureOpenAI;
            using var request = new HttpRequestMessage(HttpMethod.Post, string.Create(CultureInfo.InvariantCulture, $"/openai/deployments/{section.ModeloDespliegue}/chat/completions?api-version={section.VersionApi}"))
            {
                Content = JsonContent.Create(body),
            };
            if (!await AuthorizeAsync(request, timeout.Token))
            {
                return (_failed, false);
            }

            using var response = await http.SendAsync(request, timeout.Token);
            if (response.IsSuccessStatusCode)
            {
                return (await ParseAsync(response, timeout.Token), false);
            }

            return (_failed, response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Tiempo agotado o error de red: transitorio. Nunca se conserva el mensaje, que podría traer el punto de conexión.
            return (_failed, true);
        }
    }

    private async Task<bool> AuthorizeAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_options.AzureOpenAI.Clave))
        {
            request.Headers.Add("api-key", _options.AzureOpenAI.Clave);
            return true;
        }

        if (credential is null)
        {
            return false;
        }

        var token = await credential.GetTokenAsync(new TokenRequestContext([_scope]), cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return true;
    }

    private static async Task<ProviderResponse> ParseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = document.RootElement;
            var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            var usage = root.GetProperty("usage");
            return content is null ? _failed : new ProviderResponse(content, usage.GetProperty("prompt_tokens").GetInt32(), usage.GetProperty("completion_tokens").GetInt32());
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException or FormatException)
        {
            return _failed;
        }
    }
}
