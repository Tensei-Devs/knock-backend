using System.Net;
using System.Text;
using Knock.Contracts.Moderation;
using Knock.Market.Api.Modules.Moderation.Services;
using Knock.Market.Api.Modules.Moderation.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace Knock.UnitTest.Moderation;

internal sealed class MutableClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Servidor simulado de Content Safety: responde en cola y guarda los cuerpos recibidos (sintéticos).</summary>
internal sealed class FakeAzureHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();

    public List<string> Bodies { get; } = [];

    public int Calls => Bodies.Count;

    public FakeAzureHandler Enqueue(Func<HttpResponseMessage> response)
    {
        _responses.Enqueue(response);
        return this;
    }

    public static HttpResponseMessage Ok(int hate = 0, int sexual = 0, int violence = 0, int selfHarm = 0) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            $$"""{"categoriesAnalysis":[{"category":"Hate","severity":{{hate}}},{"category":"SelfHarm","severity":{{selfHarm}}},{"category":"Sexual","severity":{{sexual}}},{"category":"Violence","severity":{{violence}}}]}""",
            Encoding.UTF8,
            "application/json"),
    };

    public static HttpResponseMessage Status(HttpStatusCode code) => new(code);

    public static HttpResponseMessage TooMany(int retryAfterSeconds)
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(retryAfterSeconds));
        return response;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
        if (_responses.Count == 0)
        {
            throw new HttpRequestException("no response queued");
        }

        return _responses.Dequeue()();
    }
}

internal sealed class FakeConsumption : IConsumoModeracionStore
{
    private readonly Dictionary<string, CachedDecision> _cache = [];

    public CacheLookup CacheFailure { get; set; } = CacheLookup.Miss;

    public QuotaResult Quota { get; set; } = QuotaResult.Reserved;

    public long MonthlyCount { get; set; } = 1;

    public int Reservations { get; private set; }

    public int Alerts { get; private set; }

    public Task<(CacheLookup Lookup, CachedDecision? Decision)> GetCachedAsync(string kind, string fingerprint, CancellationToken cancellationToken)
    {
        if (CacheFailure == CacheLookup.Unavailable)
        {
            return Task.FromResult<(CacheLookup, CachedDecision?)>((CacheLookup.Unavailable, null));
        }

        return Task.FromResult<(CacheLookup, CachedDecision?)>(_cache.TryGetValue($"{kind}:{fingerprint}", out var hit) ? (CacheLookup.Hit, hit) : (CacheLookup.Miss, null));
    }

    public Task SetCachedAsync(string kind, string fingerprint, CachedDecision decision, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        _cache[$"{kind}:{fingerprint}"] = decision;
        return Task.CompletedTask;
    }

    public Task<QuotaReservation> ReserveAsync(string service, string user, DateOnly day, int monthlyLimit, int personLimit, CancellationToken cancellationToken)
    {
        Reservations++;
        return Task.FromResult(new QuotaReservation(Quota, MonthlyCount));
    }

    public Task<bool> TryMarkAlertAsync(string service, DateOnly day, CancellationToken cancellationToken)
    {
        Alerts++;
        return Task.FromResult(true);
    }
}

internal sealed class FakeUsage : IUsoModeracionStore
{
    public List<ModerationUsage> Saved { get; } = [];

    public Task SaveAsync(ModerationUsage usage, CancellationToken cancellationToken)
    {
        Saved.Add(usage);
        return Task.CompletedTask;
    }
}

internal sealed class ModerationHarness
{
    public ModerationHarness(Action<ModerationOptions>? configure = null)
    {
        Options = new ModerationOptions { Habilitado = true };
        Options.ContentSafety.Endpoint = "https://contentsafety.ejemplo.test";
        Options.ContentSafety.Clave = "clave-sintetica-no-real";
        configure?.Invoke(Options);
#pragma warning disable CA2000 // el cliente vive lo que la prueba
        var http = new HttpClient(Azure) { BaseAddress = new Uri(Options.ContentSafety.Endpoint) };
#pragma warning restore CA2000
        var wrapped = Microsoft.Extensions.Options.Options.Create(Options);
        var client = new ContentSafetyClient(http, wrapped, Clock);
        Moderator = new ContentSafetyModerator(client, Consumption, Usage, ListaLocal.LoadEmbedded(), wrapped, Clock, NullLogger<ContentSafetyModerator>.Instance);
    }

    public ModerationOptions Options { get; }

    public FakeAzureHandler Azure { get; } = new();

    public FakeConsumption Consumption { get; } = new();

    public FakeUsage Usage { get; } = new();

    public MutableClock Clock { get; } = new(new DateTimeOffset(2026, 11, 21, 18, 0, 0, TimeSpan.Zero));

    public ContentSafetyModerator Moderator { get; }

    public static TextModerationRequest Text(string text) => new(text, ModerationContext.MensajeChat, "sub-sintetico");

    public static ImageModerationRequest Image(int length = 1000, string type = "image/jpeg") => new(new byte[length], type, ModerationContext.ImagenPublica, "sub-sintetico");
}
