namespace Knock.Contracts.Moderation;

/// <summary>Resultado de la revisión de contenido. <see cref="Unavailable"/> significa BLOQUEADO: nunca se permite publicar si el moderador no está disponible.</summary>
public enum ModerationOutcome
{
    Allowed,
    Rejected,
    Unavailable,
}

/// <summary>Resultado de moderación con categoría opcional.</summary>
public sealed record ModerationResult(ModerationOutcome Outcome, string? Category = null);

/// <summary>
/// Contrato único de moderación (spec 013, R-05/R-06): cliente compartido para texto e imágenes.
/// Debe fallar cerrado: si el proveedor no responde devuelve <see cref="ModerationOutcome.Unavailable"/> y el llamador NO publica.
/// </summary>
public interface IContentModerator
{
    Task<ModerationResult> ReviewTextAsync(string text, CancellationToken cancellationToken);
    Task<ModerationResult> ReviewImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken);
}
