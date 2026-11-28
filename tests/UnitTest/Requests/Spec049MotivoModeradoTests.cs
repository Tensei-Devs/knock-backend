using AwesomeAssertions;
using Knock.Contracts.Moderation;
using Knock.Market.Api.Modules.Requests.Services;
using Xunit;

namespace Knock.UnitTest.Requests;

/// <summary>
/// Spec 049, R-08 (motivo «Moderado») y spec 020, R-15 (nota moderada): el moderador de notas traduce la decisión real del moderador del Market y falla cerrado
/// en lo que no sea una aprobación (KNOCK-266, A1: antes siempre respondía «no disponible» y la ruta daba 503).
/// </summary>
public sealed class Spec049MotivoModeradoTests
{
    [Theory]
    [InlineData(ModerationOutcome.Allowed, nameof(NoteModerationResult.Approved))]
    [InlineData(ModerationOutcome.Rejected, nameof(NoteModerationResult.Rejected))]
    [InlineData(ModerationOutcome.Unavailable, nameof(NoteModerationResult.Unavailable))]
    [InlineData(ModerationOutcome.LimitReached, nameof(NoteModerationResult.Unavailable))]
    public async Task Traduce_La_Decision_Del_Moderador_Y_Falla_Cerrado(ModerationOutcome outcome, string expected)
    {
        var content = new FakeContentModerator(outcome);

        var result = await new ContentNoteModerator(content).ModerateAsync("Hubo que reemplazar tambien la manguera", CancellationToken.None);

        result.ToString().Should().Be(expected);
        content.Texts.Should().Equal("Hubo que reemplazar tambien la manguera");
    }

    [Fact]
    public async Task Un_Valor_Fuera_Del_Catalogo_Se_Trata_Como_No_Disponible()
    {
        var result = await new ContentNoteModerator(new FakeContentModerator((ModerationOutcome)99)).ModerateAsync("motivo ficticio", CancellationToken.None);

        result.Should().Be(NoteModerationResult.Unavailable);
    }

    private sealed class FakeContentModerator(ModerationOutcome outcome) : IContentModerator
    {
        public List<string> Texts { get; } = [];

        public Task<ModerationResult> ReviewTextAsync(string text, CancellationToken cancellationToken)
        {
            Texts.Add(text);
            return Task.FromResult(new ModerationResult(outcome));
        }

        public Task<ModerationResult> ReviewImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("El motivo es texto: nunca se modera como imagen.");
    }
}
