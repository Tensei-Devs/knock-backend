using AwesomeAssertions;
using Knock.Market.Api.Modules.Media.Dtos;
using Knock.Market.Api.Modules.Media.Validators;
using Xunit;

namespace Knock.UnitTest.Media;

/// <summary>Spec 032, R-04 y CA-02: el cuerpo de la petición de enlace se valida por campo.</summary>
public sealed class Spec032ValidacionTests
{
    private static readonly PedirSubidaImagenRequestValidator _validador = new();

    [Fact]
    public void Acepta_Cuerpo_Valido_CA_032_02()
    {
        _validador.Validate(new PedirSubidaImagenRequest("trabajo", "image/webp", 5_242_880)).IsValid.Should().BeTrue();
        _validador.Validate(new PedirSubidaImagenRequest("perfil", "image/png", 1)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("trabajo", "image/jpeg", 5_242_881L, "tamanoBytes")]
    [InlineData("trabajo", "image/jpeg", 0L, "tamanoBytes")]
    [InlineData("trabajo", "image/svg+xml", 100L, "tipoContenido")]
    [InlineData("solicitud", "image/jpeg", 100L, "proposito")]
    [InlineData(null, "image/jpeg", 100L, "proposito")]
    public void Rechaza_Cuerpo_Invalido_CA_032_02(string? proposito, string tipo, long tamano, string campo)
    {
        var resultado = _validador.Validate(new PedirSubidaImagenRequest(proposito, tipo, tamano));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Select(e => e.PropertyName).Should().Contain(campo);
    }
}
