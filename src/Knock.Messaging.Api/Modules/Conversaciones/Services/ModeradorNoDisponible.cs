using Knock.Contracts.Moderation;

namespace Knock.Messaging.Api.Modules.Conversaciones.Services;

/// <summary>
/// Moderación por omisión de Mensajería mientras no exista la de la spec 031: siempre «no disponible», así que ningún mensaje se guarda sin revisión (falla cerrado, constitución §2.13).
/// Las pruebas la sustituyen por un doble y la spec 031 la reemplazará.
/// </summary>
internal sealed class ModeradorNoDisponible : IContentModerator
{
    public Task<ModerationResult> ReviewTextAsync(string text, CancellationToken cancellationToken) =>
        Task.FromResult(new ModerationResult(ModerationOutcome.Unavailable));

    public Task<ModerationResult> ReviewImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) =>
        Task.FromResult(new ModerationResult(ModerationOutcome.Unavailable));
}
