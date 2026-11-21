using Knock.Contracts.Moderation;
using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Common.Moderation;

/// <summary>
/// Moderador sin Azure: se usa con <c>Moderacion:Habilitado</c> apagado (por omisión) o con el modo de desarrollo <c>solo_local</c>. Reutiliza <see cref="ContentSafetyModerator"/>, que en esos modos
/// devuelve antes de tocar el cliente, Redis o MongoDB; los sustitutos de este archivo fallan cerrado si alguien los llamara.
/// </summary>
internal static class LocalOnlyModeration
{
    public static ContentSafetyModerator Create(IServiceProvider provider) => new(
        new ClosedSafetyClient(),
        new ClosedConsumptionStore(),
        new DiscardingUsageStore(),
        provider.GetRequiredService<ListaLocal>(),
        provider.GetRequiredService<IOptions<ModerationOptions>>(),
        provider.GetRequiredService<TimeProvider>(),
        provider.GetRequiredService<ILogger<ContentSafetyModerator>>());

    private sealed class ClosedSafetyClient : IContentSafetyClient
    {
        public Task<SafetyAnalysis?> AnalyzeTextAsync(string text, CancellationToken cancellationToken) => Task.FromResult<SafetyAnalysis?>(null);

        public Task<SafetyAnalysis?> AnalyzeImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) => Task.FromResult<SafetyAnalysis?>(null);
    }

    private sealed class ClosedConsumptionStore : IConsumoModeracionStore
    {
        public Task<(CacheLookup Lookup, CachedDecision? Decision)> GetCachedAsync(string kind, string fingerprint, CancellationToken cancellationToken) =>
            Task.FromResult<(CacheLookup, CachedDecision?)>((CacheLookup.Unavailable, null));

        public Task SetCachedAsync(string kind, string fingerprint, CachedDecision decision, TimeSpan lifetime, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<QuotaReservation> ReserveAsync(string service, string user, DateOnly day, int monthlyLimit, int personLimit, CancellationToken cancellationToken) =>
            Task.FromResult(new QuotaReservation(QuotaResult.Unavailable, 0));

        public Task<bool> TryMarkAlertAsync(string service, DateOnly day, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class DiscardingUsageStore : IUsoModeracionStore
    {
        public Task SaveAsync(ModerationUsage usage, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
