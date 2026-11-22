using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using Knock.Contracts.Moderation;
using Knock.Market.Api.Modules.Assistant.Services;
using Knock.Market.Api.Modules.Moderation.Data;
using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Modules.Moderation.Services;

/// <summary>
/// Coordina los pasos de la moderación (spec 031, R-03 a R-12 y R-17): longitud, lista local, filtro de datos personales, caché, topes en Redis, Azure y registro de uso. Falla cerrado: nunca devuelve
/// <see cref="ModerationDecision.Approved"/> sin una respuesta válida de Azure o de la caché, salvo en el modo de desarrollo <c>solo_local</c>.
/// </summary>
internal sealed partial class ContentSafetyModerator(
    IContentSafetyClient client,
    IConsumoModeracionStore consumption,
    IUsoModeracionStore usage,
    ListaLocal list,
    IOptions<ModerationOptions> options,
    TimeProvider clock,
    ILogger<ContentSafetyModerator> logger) : IContentModeration
{
    public const int MaxTextLength = 4000;
    public const int MaxImageBytes = 4 * 1024 * 1024;
    public static readonly TimeSpan TextCacheLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan ImageCacheLifetime = TimeSpan.FromDays(30);
    public static readonly Meter Meter = new("Knock.Moderation");
    private static readonly Counter<long> _quota80 = Meter.CreateCounter<long>("moderacion_cuota_80");
    private static readonly string[] _imageTypes = ["image/jpeg", "image/png"];

    private readonly ModerationOptions _options = options.Value;

    public Task<ModerationVerdict> ModerateTextAsync(TextModerationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return TimedAsync(request.Context, "texto", () => DecideTextAsync(request, cancellationToken), cancellationToken);
    }

    public Task<ModerationVerdict> ModerateImageAsync(ImageModerationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return TimedAsync(request.Context, "imagen", () => DecideImageAsync(request, cancellationToken), cancellationToken);
    }

    private async Task<ModerationVerdict> TimedAsync(ModerationContext context, string type, Func<Task<ModerationVerdict>> decide, CancellationToken cancellationToken)
    {
        var started = clock.GetTimestamp();
        var verdict = await decide();
        await RecordAsync(context, type, verdict, started, cancellationToken);
        return verdict;
    }

    private async Task<ModerationVerdict> DecideTextAsync(TextModerationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > MaxTextLength || string.IsNullOrWhiteSpace(request.UserId))
        {
            return ModerationVerdict.Failed(ModerationReasonCodes.NotModerable) with { Decision = ModerationDecision.Rejected };
        }

        if (list.Contiene(request.Text))
        {
            return new ModerationVerdict(ModerationDecision.Rejected, [], ModerationSource.LocalList);
        }

        if (_options.Modo == ModerationModes.LocalOnly)
        {
            return new ModerationVerdict(ModerationDecision.Approved, [], ModerationSource.LocalList);
        }

        if (!_options.Habilitado)
        {
            return ModerationVerdict.Failed(ModerationReasonCodes.Unavailable);
        }

        var censored = FiltroDatosPersonales.Censurar(request.Text).Text;
        var fingerprint = Fingerprint(System.Text.Encoding.UTF8.GetBytes(NormalizadorDeModeracion.Normalizar(censored)));
        return await ThroughCacheAsync("texto", fingerprint, TextCacheLifetime, request.UserId, () => client.AnalyzeTextAsync(censored, cancellationToken), cancellationToken);
    }

    private async Task<ModerationVerdict> DecideImageAsync(ImageModerationRequest request, CancellationToken cancellationToken)
    {
        var content = request.Content;
        if (content.IsEmpty || content.Length > MaxImageBytes || !_imageTypes.Contains(request.ContentType, StringComparer.OrdinalIgnoreCase)
            || request.Context != ModerationContext.ImagenPublica || string.IsNullOrWhiteSpace(request.UserId))
        {
            return ModerationVerdict.Failed(ModerationReasonCodes.NotModerable) with { Decision = ModerationDecision.Rejected };
        }

        if (_options.Modo == ModerationModes.LocalOnly)
        {
            return new ModerationVerdict(ModerationDecision.Approved, [], ModerationSource.LocalList);
        }

        if (!_options.Habilitado)
        {
            return ModerationVerdict.Failed(ModerationReasonCodes.Unavailable);
        }

        return await ThroughCacheAsync("imagen", Fingerprint(content.Span), ImageCacheLifetime, request.UserId, () => client.AnalyzeImageAsync(content, cancellationToken), cancellationToken);
    }

    private async Task<ModerationVerdict> ThroughCacheAsync(string kind, string fingerprint, TimeSpan lifetime, string user, Func<Task<SafetyAnalysis?>> analyze, CancellationToken cancellationToken)
    {
        var (lookup, cached) = await consumption.GetCachedAsync(kind, fingerprint, cancellationToken);
        if (lookup == CacheLookup.Unavailable)
        {
            return ModerationVerdict.Failed(ModerationReasonCodes.LimitUnavailable);
        }

        if (cached is not null && cached.ThresholdVersion == _options.VersionUmbrales)
        {
            return new ModerationVerdict(cached.Decision, cached.Categories, ModerationSource.Cache);
        }

        var today = MexicoToday();
        var reservation = await consumption.ReserveAsync(ModerationOptions.ServiceName, user, today, _options.Limites.LlamadasPorMes, _options.Limites.LlamadasPorPersonaPorDia, cancellationToken);
        switch (reservation.Result)
        {
            case QuotaResult.MonthlyExhausted:
                return ModerationVerdict.Failed(ModerationReasonCodes.QuotaExhausted);
            case QuotaResult.PersonExhausted:
                return ModerationVerdict.Failed(ModerationReasonCodes.PerPersonLimit);
            case QuotaResult.Unavailable:
                return ModerationVerdict.Failed(ModerationReasonCodes.LimitUnavailable);
        }

        await AlertIfNearLimitAsync(reservation.MonthlyCount, today, cancellationToken);
        var analysis = await analyze();
        if (analysis is null)
        {
            LogUnavailable(logger);
            return ModerationVerdict.Failed(ModerationReasonCodes.Unavailable);
        }

        var verdict = Evaluate(analysis);
        await consumption.SetCachedAsync(kind, fingerprint, new CachedDecision(_options.VersionUmbrales, verdict.Decision, verdict.Categories), lifetime, cancellationToken);
        return verdict;
    }

    /// <summary>Compara cada categoría con su umbral: la severidad igual al umbral se rechaza (spec 031, §11).</summary>
    internal ModerationVerdict Evaluate(SafetyAnalysis analysis)
    {
        var thresholds = _options.Umbrales.AsMap();
        var reached = thresholds.Where(pair => analysis.Severities.TryGetValue(pair.Key, out var severity) && severity >= pair.Value).Select(pair => pair.Key).ToArray();
        return new ModerationVerdict(reached.Length == 0 ? ModerationDecision.Approved : ModerationDecision.Rejected, reached, ModerationSource.Azure);
    }

    private async Task AlertIfNearLimitAsync(long monthlyCount, DateOnly today, CancellationToken cancellationToken)
    {
        if (monthlyCount * 100 >= (long)_options.Limites.LlamadasPorMes * 80 && await consumption.TryMarkAlertAsync(ModerationOptions.ServiceName, today, cancellationToken))
        {
            _quota80.Add(1);
        }
    }

    private async Task RecordAsync(ModerationContext context, string type, ModerationVerdict verdict, long started, CancellationToken cancellationToken)
    {
        var latency = (int)clock.GetElapsedTime(started).TotalMilliseconds;
        try
        {
            await usage.SaveAsync(new ModerationUsage(ModerationOptions.ServiceName, context, type, verdict.Decision, verdict.Source, verdict.Categories, verdict.ReasonCode, latency, clock.GetUtcNow().UtcDateTime), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // El registro de uso nunca cambia la decisión; solo se anota el tipo de fallo, sin contenido.
            LogUsageFailed(logger, ex.GetType().Name);
        }
    }

    private DateOnly MexicoToday() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), MexicoCity).DateTime);

    internal static TimeZoneInfo MexicoCity { get; } = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    private static string Fingerprint(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    [LoggerMessage(Level = LogLevel.Warning, Message = "Content Safety did not return a valid response; the content was not published.")]
    private static partial void LogUnavailable(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not save moderation usage ({ErrorType}).")]
    private static partial void LogUsageFailed(ILogger logger, string errorType);
}
