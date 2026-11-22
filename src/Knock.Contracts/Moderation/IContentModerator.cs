namespace Knock.Contracts.Moderation;

/// <summary>Resultado de la revisión de contenido. <see cref="Unavailable"/> significa BLOQUEADO: nunca se permite publicar si el moderador no está disponible. Es 0 para que un valor sin asignar falle cerrado.</summary>
public enum ModerationOutcome
{
    Unavailable = 0,
    Allowed = 1,
    Rejected = 2,
    /// <summary>La persona llegó a su tope diario de moderación (spec 031, R-13): el llamador responde 429 <c>moderation_limit</c> con <c>Retry-After</c>. Nada se publica.</summary>
    LimitReached = 3,
}

/// <summary>Resultado de moderación con categoría opcional y, cuando no se publica por falla o tope, los segundos que el llamador devuelve en <c>Retry-After</c>.</summary>
public sealed record ModerationResult(ModerationOutcome Outcome, string? Category = null, int? RetryAfterSeconds = null);

/// <summary>
/// Contrato único de moderación (spec 013, R-05/R-06): cliente compartido para texto e imágenes.
/// Debe fallar cerrado: si el proveedor no responde devuelve <see cref="ModerationOutcome.Unavailable"/> y el llamador NO publica.
/// </summary>
public interface IContentModerator
{
    Task<ModerationResult> ReviewTextAsync(string text, CancellationToken cancellationToken);
    Task<ModerationResult> ReviewImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken);
}
