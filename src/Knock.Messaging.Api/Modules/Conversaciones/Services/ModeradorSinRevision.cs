using Knock.Contracts.Moderation;

namespace Knock.Messaging.Api.Modules.Conversaciones.Services;

/// <summary>
/// Modo explícito sin moderación (solo Development, <c>Moderacion:PermitirSinModeracion</c>): aprueba cualquier texto para poder probar el chat mientras la moderación real de la spec 031 no llega a Mensajería.
/// Nunca se registra salvo que el interruptor esté encendido; el valor por omisión es <see cref="ModeradorNoDisponible"/>.
/// </summary>
internal sealed class ModeradorSinRevision : IContentModerator
{
    public Task<ModerationResult> ReviewTextAsync(string text, CancellationToken cancellationToken) =>
        Task.FromResult(new ModerationResult(ModerationOutcome.Allowed));

    public Task<ModerationResult> ReviewImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) =>
        Task.FromResult(new ModerationResult(ModerationOutcome.Allowed));
}
