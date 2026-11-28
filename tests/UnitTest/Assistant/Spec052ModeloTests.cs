using AwesomeAssertions;
using Knock.Market.Api.Modules.Assistant.Services;
using Xunit;

namespace Knock.UnitTest.Assistant;

/// <summary>Spec 052, CA-31: lo que llega al modelo ya no trae teléfonos ni direcciones.</summary>
public sealed class Spec052ModeloTests
{
    [Fact]
    public async Task Modelo_Recibe_El_Texto_Sin_Telefono_Ni_Direccion_CA_052_31()
    {
        var harness = new AiHarness();
        harness.Azure.Enqueue(() => AiHandler.Ok(AiHarness.ValidAssistant));

        var result = await harness.Client.GenerarAsync<AsistenteKnockSalida>(
            new SolicitudIa(AiFunction.AsistenteKnock, "sub-sintetico", "Gotea mi llave, llámame al 55 5555 1234, vivo en Calle Hidalgo 123", []), CancellationToken.None);

        result.MotivoRespaldo.Should().BeNull();
        harness.Azure.Calls.Should().Be(1);
        harness.Azure.Bodies[0].Should().NotContain("5555 1234").And.NotContain("Hidalgo 123");
        result.TiposCensurados.Should().NotBeEmpty();
    }
}
