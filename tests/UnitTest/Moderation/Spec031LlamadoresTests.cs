using System.Security.Claims;
using AwesomeAssertions;
using Knock.Contracts.Moderation;
using Knock.Market.Api.Modules.Moderation;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Knock.UnitTest.Moderation;

/// <summary>Spec 031, T-09 / R-13: los llamadores existentes reciben el tope por persona como 429 y no como falla, con los segundos del traductor único.</summary>
public sealed class Spec031LlamadoresTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 21, 20, 0, 0, TimeSpan.Zero);

    private sealed class FixedModeration(ModerationVerdict verdict) : IContentModeration
    {
        public Task<ModerationVerdict> ModerateTextAsync(TextModerationRequest request, CancellationToken cancellationToken) => Task.FromResult(verdict);

        public Task<ModerationVerdict> ModerateImageAsync(ImageModerationRequest request, CancellationToken cancellationToken) => Task.FromResult(verdict);
    }

    private static LegacyContentModeratorAdapter Adapter(ModerationVerdict verdict, bool signedIn = true)
    {
        var context = new DefaultHttpContext();
        if (signedIn)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-1")], "test"));
        }

        return new LegacyContentModeratorAdapter(new FixedModeration(verdict), new HttpContextAccessor { HttpContext = context }, new MutableClock(_now));
    }

    [Fact]
    public async Task Tope_Por_Persona_Es_LimitReached_Con_Segundos_Hasta_La_Medianoche_CA_031_09()
    {
        var result = await Adapter(ModerationVerdict.Failed(ModerationReasonCodes.PerPersonLimit)).ReviewTextAsync("hola", CancellationToken.None);

        result.Outcome.Should().Be(ModerationOutcome.LimitReached);
        result.RetryAfterSeconds.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(24 * 3600);
    }

    [Theory]
    [InlineData(ModerationReasonCodes.Unavailable)]
    [InlineData(ModerationReasonCodes.QuotaExhausted)]
    [InlineData(ModerationReasonCodes.LimitUnavailable)]
    public async Task Otras_Fallas_Siguen_Siendo_Unavailable_Con_30_Segundos_CA_031_05(string reason)
    {
        var result = await Adapter(ModerationVerdict.Failed(reason)).ReviewImageAsync(new byte[] { 1 }, CancellationToken.None);

        result.Outcome.Should().Be(ModerationOutcome.Unavailable);
        result.RetryAfterSeconds.Should().Be(30);
    }

    [Fact]
    public async Task Aprobado_Y_Rechazado_No_Llevan_Espera()
    {
        var approved = await Adapter(new ModerationVerdict(ModerationDecision.Approved, [])).ReviewTextAsync("hola", CancellationToken.None);
        var rejected = await Adapter(new ModerationVerdict(ModerationDecision.Rejected, ["hate"])).ReviewTextAsync("hola", CancellationToken.None);

        approved.Should().Be(new ModerationResult(ModerationOutcome.Allowed));
        rejected.Outcome.Should().Be(ModerationOutcome.Rejected);
        rejected.RetryAfterSeconds.Should().BeNull();
    }

    [Fact]
    public async Task Sin_Sesion_Falla_Cerrado_Sin_Llamar_Al_Proveedor()
    {
        var result = await Adapter(new ModerationVerdict(ModerationDecision.Approved, []), signedIn: false).ReviewTextAsync("hola", CancellationToken.None);

        result.Outcome.Should().Be(ModerationOutcome.Unavailable);
    }
}
