using AwesomeAssertions;
using Knock.Market.Api.Common.Text;
using Knock.Market.Api.Modules.Tecnicos.Domain;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Tecnicos;

public sealed class Spec027CamposDeBusquedaTests
{
    [Theory]
    [InlineData("Ana Núñez", "Plomería Ana", "ana,nuñez,plomeria")]
    [InlineData("  José   Pérez ", null, "jose,perez")]
    [InlineData("Luis", "A-1 Frenos & Más", "luis,a,1,frenos,mas")]
    public void Palabras_Son_Las_Del_Nombre_Y_El_Negocio_Sin_Repetir_CA_027_04(string name, string? business, string expected) =>
        TechnicianSearchFields.WordsOf(name, business).Should().Equal(expected.Split(','));

    [Fact]
    public void Las_Palabras_Usan_El_Mismo_Normalizador_Que_La_Consulta_CA_027_04() =>
        TechnicianSearchFields.WordsOf("Óscar", "Fontanería Piñón")
            .Should().Equal(SearchNameNormalizer.NormalizeQuery("Óscar Fontanería Piñón").Split(' '));

    [Fact]
    public void Agrega_Nombre_Publico_Y_Palabras_CA_027_04()
    {
        var document = new BsonDocument();

        TechnicianSearchFields.AddTo(document, "Ana Pruebas", null);

        document["nombrePublico"].AsString.Should().Be("Ana Pruebas");
        document["palabrasBusqueda"].AsBsonArray.Select(x => x.AsString).Should().Equal("ana", "pruebas");
    }
}
