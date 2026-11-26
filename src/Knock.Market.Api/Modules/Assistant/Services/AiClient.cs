using System.Globalization;
using System.Text.Json;
using Knock.Market.Api.Modules.Assistant.Data;
using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Modules.Assistant.Services;

/// <summary>
/// Orquesta una generación (spec 024, R-01 a R-14): apagado, filtro de datos personales, imágenes por <c>archivoId</c>, reserva atómica de tokens, proveedor, validación del esquema cerrado, ajuste de la
/// reserva, registro de uso sin contenido y, si algo falla, plantilla local con el mismo esquema. Nunca lanza por fallos del proveedor: siempre devuelve un valor con su origen.
/// </summary>
internal sealed partial class AiClient(
    IProveedorIa provider,
    IAiImageSource images,
    IConsumoIa consumption,
    IUsoIa usage,
    PlantillaIa templates,
    IOptions<AssistantOptions> options,
    TimeProvider clock,
    ILogger<AiClient> logger) : IAiClient
{
    public const int MaxInputChars = 4000;
    public const int MaxOutputFieldChars = 500;
    public const int MaxImages = 3;
    private const int _tokensPerImage = 765;
    private const int _charsPerToken = 3;
    private static readonly TimeZoneInfo _mexicoCity = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    private readonly AssistantOptions _options = options.Value;

    public async Task<ResultadoIa<T>> GenerarAsync<T>(SolicitudIa solicitud, CancellationToken cancellationToken) where T : class, IAiOutput<T>
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var started = clock.GetTimestamp();
        var filtered = FiltroDatosPersonales.Censurar(solicitud.Texto);
        var types = filtered.Types.Select(entry => new CensorshipSummary(entry.Type, entry.Count)).ToList();
        var text = filtered.Text;
        var clipped = text.Length > MaxInputChars;
        if (clipped)
        {
            // Se filtra el texto completo y solo después se recorta, para no partir un dato por la mitad (R-06).
            text = text[..MaxInputChars];
        }

        var prompt = templates.Prompt(AiOutputs.FunctionOf<T>());
        var (value, reason, tokensIn, tokensOut) = await TryProviderAsync<T>(solicitud, prompt, text, cancellationToken);
        var origin = value is null ? AiOrigin.Template : AiOrigin.Provider;
        if (value is null)
        {
            value = templates.Template<T>();
            reason ??= FallbackReasons.Unavailable;
        }
        else
        {
            value = value.Clip(MaxOutputFieldChars, out var outputClipped);
            clipped |= outputClipped;
            reason = null;
        }

        var latency = (int)clock.GetElapsedTime(started).TotalMilliseconds;
        await RecordAsync(solicitud, origin, prompt.Version, reason, tokensIn, tokensOut, latency, clipped, cancellationToken);
        return value is null
            ? new ResultadoIa<T>(null, AiOrigin.Template, FallbackReasons.NoFallback, types, clipped, latency)
            : new ResultadoIa<T>(value, origin, reason, types, clipped, latency);
    }

    private async Task<(T? Value, string? Reason, int In, int Out)> TryProviderAsync<T>(SolicitudIa request, PromptIa prompt, string text, CancellationToken cancellationToken) where T : class, IAiOutput<T>
    {
        if (!_options.Habilitado)
        {
            return (null, FallbackReasons.Disabled, 0, 0);
        }

        var pictures = await LoadImagesAsync(request, cancellationToken);
        if (pictures is null)
        {
            return (null, FallbackReasons.ImageRejected, 0, 0);
        }

        var reserve = ((prompt.Text.Length + text.Length) / _charsPerToken) + (pictures.Count * _tokensPerImage) + _options.Limites.TokensSalidaMaximos;
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), _mexicoCity).DateTime);
        var reservation = await consumption.ReserveAsync(request.UsuarioId, day, reserve, _options.Limites.TokensPorUsuarioPorDia, _options.Limites.TokensGlobalesPorDia, cancellationToken);
        if (reservation.Reservation is null)
        {
            return (null, reservation.Result == ConsumptionResult.BudgetExhausted ? FallbackReasons.BudgetExhausted : FallbackReasons.LimitUnavailable, 0, 0);
        }

        var response = await provider.CompleteAsync(prompt.Text, text, pictures, _options.Limites.TokensSalidaMaximos, cancellationToken);
        var actual = response.Json is null ? 0 : response.InputTokens + response.OutputTokens;
        if (await consumption.AdjustAsync(reservation.Reservation, actual, cancellationToken) == AdjustmentResult.Unavailable)
        {
            LogAdjustFailed(logger);
        }

        if (response.Json is null)
        {
            return (null, FallbackReasons.Unavailable, 0, 0);
        }

        var parsed = Parse<T>(response.Json);
        return parsed is null ? (null, FallbackReasons.InvalidSchema, response.InputTokens, response.OutputTokens) : (parsed, null, response.InputTokens, response.OutputTokens);
    }

    private async Task<List<byte[]>?> LoadImagesAsync(SolicitudIa request, CancellationToken cancellationToken)
    {
        var pictures = new List<byte[]>();
        foreach (var id in request.ArchivoIds.Take(MaxImages))
        {
            if (!Guid.TryParse(id, CultureInfo.InvariantCulture, out var fileId) || await images.GetReducedImageAsync(fileId, request.UsuarioId, cancellationToken) is not { } bytes)
            {
                return null;
            }

            pictures.Add(bytes);
        }

        return pictures;
    }

    private static T? Parse<T>(string json) where T : class, IAiOutput<T>
    {
        try
        {
            var value = JsonSerializer.Deserialize<T>(json, AiText.Strict);
            return value is not null && value.Clip(MaxOutputFieldChars, out _).IsValid() ? value : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task RecordAsync(SolicitudIa request, AiOrigin origin, string promptVersion, string? reason, int tokensIn, int tokensOut, int latency, bool clipped, CancellationToken cancellationToken)
    {
        var model = origin == AiOrigin.Provider ? _options.AzureOpenAI.ModeloDespliegue ?? "desconocido" : "plantilla";
        try
        {
            await usage.SaveAsync(
                new UsageRecord(request.UsuarioId, AiText.Name(request.Funcion), model, promptVersion, tokensIn, tokensOut, latency, origin == AiOrigin.Provider ? "proveedor" : "plantilla", reason, clipped, clock.GetUtcNow().UtcDateTime),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // El registro de uso nunca cambia la respuesta; solo el tipo de fallo, sin contenido.
            LogUsageFailed(logger, ex.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not adjust the AI token reservation.")]
    private static partial void LogAdjustFailed(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not save AI usage ({ErrorType}).")]
    private static partial void LogUsageFailed(ILogger logger, string errorType);
}
