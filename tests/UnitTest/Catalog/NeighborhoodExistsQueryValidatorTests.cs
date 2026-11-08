using AwesomeAssertions;
using Knock.Market.Api.Modules.Catalog.Dtos;
using Knock.Market.Api.Modules.Catalog.ModelBinders;
using Knock.Market.Api.Modules.Catalog.Validators;
using Xunit;

namespace Knock.UnitTest.Catalog;

/// <summary>Spec 017, R-06: la consulta «¿existe esta colonia?» valida el nombre y nunca repite el valor recibido.</summary>
public class NeighborhoodExistsQueryValidatorTests
{
    private static readonly NeighborhoodExistsQueryValidator _validator = new();

    [Theory]
    [InlineData("Centro")]
    [InlineData("El Llano 1a Sección")]
    [InlineData("San José (norte)")]
    [InlineData("A")]
    public void ValidNames_Pass_CA_017_07(string name)
    {
        _validator.Validate(new NeighborhoodExistsQuery { Nombre = name }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" Centro")]
    [InlineData("<script>")]
    [InlineData("a\nb")]
    [InlineData("Centro\r\n")]
    [InlineData("{\"$ne\":null}")]
    [InlineData(SingleValueQueryModelBinder.RepeatedValue)]
    public void InvalidNames_Fail_CA_017_07(string? name)
    {
        _validator.Validate(new NeighborhoodExistsQuery { Nombre = name }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void NameLongerThanTheLimit_Fails_CA_017_07()
    {
        _validator.Validate(new NeighborhoodExistsQuery { Nombre = new string('a', NeighborhoodExistsQuery.MaxLength + 1) }).IsValid.Should().BeFalse();
        _validator.Validate(new NeighborhoodExistsQuery { Nombre = new string('a', NeighborhoodExistsQuery.MaxLength) }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Reasons_NeverRepeatTheReceivedValue_CA_017_07()
    {
        var result = _validator.Validate(new NeighborhoodExistsQuery { Nombre = "<secreto-que-no-debe-salir>" });

        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().OnlyContain(e => !e.ErrorMessage.Contains("secreto", StringComparison.Ordinal));
    }
}
