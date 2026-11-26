using AwesomeAssertions;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>La búsqueda del código de visita en una respuesta (spec 022, CA-02): sin falsos positivos con el precio sembrado y sin dejar pasar una fuga. Sin contenedores.</summary>
public sealed class Spec022BusquedaDelCodigoTests
{
    [Fact]
    public void El_Precio_Sembrado_No_Es_El_Codigo_CA_022_02()
    {
        const string view = """{"rol":"tecnico","precio":1000,"tope":1000,"hora":"10:00"}""";

        Spec020Host.CarriesValue(view, "1000").Should().BeTrue("así fallaba la prueba cuando el código salía 1000");
        Spec020Host.CarriesVisitCode(view, "1000").Should().BeFalse();
    }

    [Theory]
    [InlineData("""{"precio":1000,"tope":1000,"codigo":"1000"}""", "1000")]
    [InlineData("""{"precio":1000,"visita":{"clave":1000}}""", "1000")]
    [InlineData("""{"precio":"1000"}""", "1000")]
    [InlineData("""{"precio":4521,"tope":1000}""", "4521")]
    [InlineData("""{"tope":1000,"datos":[{"x":"0457"}]}""", "0457")]
    public void Una_Fuga_Del_Codigo_Se_Sigue_Detectando_CA_022_02(string json, string code) =>
        Spec020Host.CarriesVisitCode(json, code).Should().BeTrue();
}
