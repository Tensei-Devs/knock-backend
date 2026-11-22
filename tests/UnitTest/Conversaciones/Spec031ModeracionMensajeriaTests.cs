extern alias messaging;
using AwesomeAssertions;
using Knock.Contracts.Moderation;
using messaging::Knock.Messaging.Api.Modules.Conversaciones.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;
using Xunit;

namespace Knock.UnitTest.Conversaciones;

/// <summary>Spec 031 en Mensajería: el modo sin moderación es explícito, apagado por omisión y solo existe en Development. Solo datos ficticios.</summary>
public sealed class Spec031ModeracionMensajeriaTests
{
    private static ValidateOptionsResult Validate(bool permitir, string environment)
    {
        var host = new HostingEnvironment { EnvironmentName = environment };
        return new ModeracionMensajeriaOptionsValidator(host).Validate(null, new ModeracionMensajeriaOptions { PermitirSinModeracion = permitir });
    }

    [Fact]
    public void Por_Omision_El_Interruptor_Esta_Apagado() =>
        new ModeracionMensajeriaOptions().PermitirSinModeracion.Should().BeFalse();

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Encendido_Fuera_De_Development_Falla_Sin_Mostrar_Valores(string environment)
    {
        var result = Validate(true, environment);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Moderacion:PermitirSinModeracion");
    }

    [Theory]
    [InlineData(true, "Development")]
    [InlineData(false, "Production")]
    [InlineData(false, "Development")]
    public void Combinaciones_Permitidas_Arrancan(bool permitir, string environment) =>
        Validate(permitir, environment).Succeeded.Should().BeTrue();

    [Fact]
    public async Task Sin_Revision_Permite_Y_No_Disponible_Bloquea()
    {
        var open = await new ModeradorSinRevision().ReviewTextAsync("hola", CancellationToken.None);
        var closed = await new ModeradorNoDisponible().ReviewTextAsync("hola", CancellationToken.None);

        open.Outcome.Should().Be(ModerationOutcome.Allowed);
        closed.Outcome.Should().Be(ModerationOutcome.Unavailable);
    }
}
