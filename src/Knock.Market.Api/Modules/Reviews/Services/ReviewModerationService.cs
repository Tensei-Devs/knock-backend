using System.Collections.Concurrent;
using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Modules.Reviews.Data;
using Knock.Market.Api.Modules.Reviews.Domain;
using MongoDB.Bson;

namespace Knock.Market.Api.Modules.Reviews.Services;

/// <summary>Generación de la caché de lectura de cada técnico (spec 061, R-05): subirla deja sin uso todas las páginas guardadas de esa réplica, sea cual sea su cursor o límite.</summary>
internal static class ReviewCacheGenerations
{
    private static readonly ConcurrentDictionary<ObjectId, long> _generations = new();

    public static long Current(ObjectId technicianId) => _generations.TryGetValue(technicianId, out var value) ? value : 0;

    public static void Bump(ObjectId technicianId) => _generations.AddOrUpdate(technicianId, 1, (_, value) => value + 1);
}

/// <summary>
/// Ocultar y volver a mostrar una opinión desde el panel (spec 061). La persona es la del <c>sub</c> del token del panel. No toca MongoDB: pasa por <see cref="IReviewStore"/>, que recalcula la reputación
/// y escribe el evento en la misma transacción. Una opinión que no cambia no sube la generación de la caché ni escribe otro evento.
/// </summary>
internal sealed class ReviewModerationService(IReviewStore store, TimeProvider clock)
{
    public Task HideAsync(string moderatorId, ObjectId reviewId, string reason, CancellationToken cancellationToken) =>
        SetAsync(moderatorId, reviewId, true, reason.Trim(), cancellationToken);

    public Task ShowAsync(string moderatorId, ObjectId reviewId, CancellationToken cancellationToken) =>
        SetAsync(moderatorId, reviewId, false, null, cancellationToken);

    private async Task SetAsync(string moderatorId, ObjectId reviewId, bool hidden, string? reason, CancellationToken cancellationToken)
    {
        var result = await store.SetHiddenAsync(reviewId, hidden, reason, moderatorId, clock.GetUtcNow().UtcDateTime, cancellationToken);
        if (result.Outcome == ReviewModerationOutcome.NotFound)
        {
            throw new DomainException(DomainErrorKind.NotFound, "The review does not exist.", ReviewRules.ReviewNotFoundCode);
        }

        if (result.Outcome == ReviewModerationOutcome.Changed && result.TechnicianId is { } technician)
        {
            ReviewCacheGenerations.Bump(technician);
        }
    }
}
