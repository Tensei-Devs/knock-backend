using Knock.Contracts.Moderation;

namespace Knock.Market.Api.Common.Moderation;

/// <summary>
/// Traducción única de la decisión del moderador de contenido a la que usa cada módulo del Market (notas y motivos de la spec 020 y 049, opiniones de la 051).
/// Falla cerrado: lo que no sea <see cref="ModerationOutcome.Allowed"/> ni <see cref="ModerationOutcome.Rejected"/> (no disponible, tope diario o un valor desconocido) toma el valor <c>otherwise</c>.
/// </summary>
internal static class ModerationOutcomes
{
    public static async Task<T> ReviewTextAsync<T>(this IContentModerator moderator, string text, T allowed, T rejected, T otherwise, CancellationToken cancellationToken) =>
        (await moderator.ReviewTextAsync(text, cancellationToken)).Outcome switch
        {
            ModerationOutcome.Allowed => allowed,
            ModerationOutcome.Rejected => rejected,
            _ => otherwise,
        };
}
