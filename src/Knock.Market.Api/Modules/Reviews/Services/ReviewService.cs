using Knock.Contracts.Moderation;
using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Common.TareasProgramadas;
using Knock.Market.Api.Modules.Media;
using Knock.Market.Api.Modules.Reviews.Data;
using Knock.Market.Api.Modules.Reviews.Domain;
using Knock.Market.Api.Modules.Reviews.Dtos;
using Knock.Market.Api.Modules.Reviews.Validators;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;

namespace Knock.Market.Api.Modules.Reviews.Services;

/// <summary>
/// Opiniones y reputación del técnico (spec 051, OPI-01 a OPI-03). La persona es siempre la del <c>sub</c> del token (R-18). El cliente califica una sola vez su visita terminada dentro de 30
/// días; el comentario pasa por la moderación (spec 031) y la calificación cuenta desde que se envía (D-04). No toca MongoDB: pasa por <see cref="IReviewStore"/>.
/// </summary>
internal sealed class ReviewService(IReviewStore store, IContentModerator moderator, IMediaApi media, IMemoryCache cache, TimeProvider clock)
{
    /// <summary>Vida de la primera página de opiniones en la caché de la réplica (R-05 y R-16).</summary>
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(1);

    /// <summary>Clave de la lista: solo lleva el técnico, la página y el límite; nunca datos de quien consulta (R-05).</summary>
    public static string CacheKey(ObjectId technicianId, string? cursor, int limit) => $"opiniones:{technicianId}:{cursor}:{limit}";

    public async Task<ReviewPage> ListAsync(string technicianId, string? cursor, int limit, CancellationToken cancellationToken)
    {
        var id = ObjectId.Parse(technicianId);
        var key = CacheKey(id, cursor, limit);
        if (cache.TryGetValue(key, out ReviewPage? cached) && cached is not null)
        {
            return cached;
        }

        if (!await store.IsTechnicianVisibleAsync(id, cancellationToken))
        {
            throw new DomainException(DomainErrorKind.NotFound, "The technician does not exist or is hidden.", ReviewRules.TechnicianNotFound);
        }

        (DateTime, ObjectId)? after = cursor is not null && ReviewFormat.TryDecodeCursor(cursor, out var at, out var last) ? (at, last) : null;
        var page = await store.ListAsync(id, after, limit, cancellationToken);
        cache.Set(key, page, CacheLifetime);
        return page;
    }

    public async Task<CreatedReview> CreateAsync(string sub, string visitId, CreateReviewBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var visit = await store.FindVisitAsync(ObjectId.Parse(visitId), cancellationToken)
            ?? throw new DomainException(DomainErrorKind.NotFound, "The visit does not exist.", ReviewRules.VisitNotFoundCode);
        if (!string.Equals(visit.ClientId, sub, StringComparison.Ordinal))
        {
            throw new DomainException(DomainErrorKind.Forbidden, "Only the client of the visit can review it.", ReviewRules.ForbiddenCode);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (visit.State != "terminada" || visit.FinishedAtUtc is not { } finished || !ReviewRules.IsWithinPeriod(now, finished))
        {
            throw NotAllowed();
        }

        var photos = body.Fotos ?? [];
        foreach (var photo in photos)
        {
            if (!await media.IsReadyAndOwnedByAsync(Guid.ParseExact(photo, "D"), sub, cancellationToken))
            {
                throw new DomainException(DomainErrorKind.Unprocessable, "A photo is not available.", ErrorCodes.ValidationFailedCode);
            }
        }

        var comment = string.IsNullOrWhiteSpace(body.Comentario) ? null : body.Comentario.Trim();
        var moderation = comment is null ? ReviewRules.Published : await ModerateAsync(comment, cancellationToken);
        var stored = comment is not null && moderation == ReviewRules.Rejected ? null : comment;
        var review = new NewReview(visit.Id, visit.RequestId, visit.TechnicianId, sub, body.Calificacion!.Value, body.Partes, stored, photos, moderation, body.GuardarTecnicoConfianza == true);
        var id = await store.CreateAsync(review, now, cancellationToken) ?? throw NotAllowed();
        cache.Remove(CacheKey(visit.TechnicianId, null, ReviewRules.DefaultLimit));
        return new CreatedReview(id.ToString(), review.Stars, moderation, now);
    }

    public async Task AnswerAsync(string sub, string reviewId, AnswerReviewBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var review = await store.FindReviewAsync(ObjectId.Parse(reviewId), cancellationToken)
            ?? throw new DomainException(DomainErrorKind.NotFound, "The review does not exist.", ReviewRules.ReviewNotFoundCode);
        if (await store.FindTechnicianByUserAsync(sub, cancellationToken) != review.TechnicianId)
        {
            throw new DomainException(DomainErrorKind.Forbidden, "Only the reviewed technician can answer.", ReviewRules.ForbiddenCode);
        }

        var text = body.Texto!.Trim();
        if (review.Answered || await ModerateAsync(text, cancellationToken) == ReviewRules.Rejected)
        {
            throw review.Answered ? Answered() : new DomainException(DomainErrorKind.Unprocessable, "The answer was not accepted.", ErrorCodes.ValidationFailedCode);
        }

        if (!await store.AnswerAsync(review, text, clock.GetUtcNow().UtcDateTime, cancellationToken))
        {
            throw Answered();
        }

        cache.Remove(CacheKey(review.TechnicianId, null, ReviewRules.DefaultLimit));
    }

    private async Task<string> ModerateAsync(string text, CancellationToken cancellationToken) => (await moderator.ReviewTextAsync(text, cancellationToken)).Outcome switch
    {
        ModerationOutcome.Allowed => ReviewRules.Published,
        ModerationOutcome.Rejected => ReviewRules.Rejected,
        _ => ReviewRules.Pending,
    };

    private static DomainException NotAllowed() => new(DomainErrorKind.Conflict, "The visit cannot be reviewed.", ReviewRules.ReviewNotAllowedCode);

    private static DomainException Answered() => new(DomainErrorKind.Conflict, "The review already has an answer.", ReviewRules.ReviewAlreadyAnsweredCode);
}

/// <summary>Tarea <c>CerrarPeriodoDeOpinion</c> (spec 054, 30 días después de terminada la visita): cierra el plazo de opinar. Una visita ya calificada lo cierra igual, sin efecto.</summary>
internal sealed class CerrarPeriodoDeOpinion(IReviewStore store, TimeProvider clock) : IManejadorDeTarea
{
    public string Tipo => ReviewRules.CloseOpinionTask;

    public Task HandleAsync(TareaReservada task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        return ObjectId.TryParse(task.ClaveNegocio, out var id)
            ? store.CloseOpinionPeriodAsync(id, clock.GetUtcNow().UtcDateTime, cancellationToken)
            : throw new FormatException("Invalid opinion task key.");
    }
}
