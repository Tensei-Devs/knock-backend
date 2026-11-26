namespace Knock.Contracts.Moderation;

/// <summary>Decisión de moderación (spec 031, R-01). <see cref="Unavailable"/> es 0 para que un valor sin asignar falle cerrado: lo que no se pudo revisar no se publica.</summary>
public enum ModerationDecision
{
    Unavailable = 0,
    Approved = 1,
    Rejected = 2,
}

/// <summary>Quién tomó la decisión: Azure Content Safety, la lista local o la caché de decisiones.</summary>
public enum ModerationSource
{
    Azure,
    LocalList,
    Cache,
}

/// <summary>Dónde se publica el contenido (spec 031, R-01).</summary>
public enum ModerationContext
{
    SolicitudDescripcion,
    CotizacionNota,
    MensajeChat,
    ImagenPublica,
}

/// <summary>Texto a moderar. <paramref name="UserId"/> es el <c>sub</c> del token y solo se usa para el tope diario por persona: nunca sale hacia Azure.</summary>
public sealed record TextModerationRequest(string Text, ModerationContext Context, string UserId);

/// <summary>Imagen ya procesada por la spec 032 (JPEG recodificado, sin metadatos).</summary>
public sealed record ImageModerationRequest(ReadOnlyMemory<byte> Content, string ContentType, ModerationContext Context, string UserId);

/// <summary>Resultado de moderar. <see cref="Categories"/> lista solo las categorías que alcanzaron el umbral; <see cref="ReasonCode"/> solo se llena cuando no hubo respuesta de Azure o la entrada no se podía moderar.</summary>
public sealed record ModerationVerdict(ModerationDecision Decision, IReadOnlyList<string> Categories, ModerationSource? Source = null, string? ReasonCode = null)
{
    public static ModerationVerdict Failed(string reasonCode) => new(ModerationDecision.Unavailable, [], null, reasonCode);
}

/// <summary>Códigos de motivo de <see cref="ModerationVerdict.ReasonCode"/> (spec 031, §6).</summary>
public static class ModerationReasonCodes
{
    public const string NotModerable = "not_moderable";
    public const string Unavailable = "unavailable";
    public const string QuotaExhausted = "quota_exhausted";
    public const string PerPersonLimit = "per_person_limit";
    public const string LimitUnavailable = "limit_unavailable";
}

/// <summary>
/// Contrato de moderación de la spec 031: un solo punto para texto e imágenes que falla cerrado. Convive con <see cref="IContentModerator"/> (spec 013), que la implementación real
/// también cumple mediante un adaptador, de modo que los llamadores existentes no cambian.
/// </summary>
public interface IContentModeration
{
    Task<ModerationVerdict> ModerateTextAsync(TextModerationRequest request, CancellationToken cancellationToken);

    Task<ModerationVerdict> ModerateImageAsync(ImageModerationRequest request, CancellationToken cancellationToken);
}
