using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.Market.Api.Modules.Catalog.Domain;
using Knock.Market.Api.Modules.Catalog.Services;
using Xunit;

namespace Knock.UnitTest.Catalog;

public sealed class Spec027SearchSafetyTests
{
    [Theory]
    [InlineData("una búsqueda cualquiera")]
    [InlineData("correo@ejemplo.test")]
    [InlineData("calle inventada 123")]
    [InlineData("5555550101")]
    public void Filtro_No_Disponible_Impide_Contar_R_027_12(string term)
    {
        var filter = new FiltroDatosPersonalesNoDisponible();
        filter.CanCount(term).Should().BeFalse();
    }

    [Fact]
    public void Clave_Cursor_Exige_256_Bits_CA_027_11()
    {
        var validator = new SearchCursorOptionsValidator();
        validator.Validate(null, new SearchCursorOptions()).Failed.Should().BeTrue();
        validator.Validate(null, new SearchCursorOptions { Key = "no-base64" }).Failed.Should().BeTrue();
        validator.Validate(null, new SearchCursorOptions { Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(31)) }).Failed.Should().BeTrue();
        validator.Validate(null, new SearchCursorOptions { Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Estado_Del_Cursor_Para_Paginacion_En_Base_CA_027_11()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var query = new SearchQuery("boiler", "colonia", null, false, 1);
        var last = new SearchRank("id-demo", 1, true, 4, 1.2);
        var token = CursorDeBusqueda.CreateNext(query, last, 20, key);
        var state = CursorDeBusqueda.Read(token, query, key);
        state.Last.Should().Be(last);
        state.Count.Should().Be(20);
        Action wrong = () => CursorDeBusqueda.Read(token, query with { DictionaryVersion = 2 }, key);
        wrong.Should().Throw<FormatException>();
        Action invalidCount = () => CursorDeBusqueda.CreateNext(query, last, 100, key);
        invalidCount.Should().Throw<ArgumentOutOfRangeException>();
    }
}
