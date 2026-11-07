using AwesomeAssertions;
using Knock.DatabaseTest.Support;
using Knock.Market.Api.Modules.Catalog.Data;
using Knock.Market.Api.Modules.Catalog.Domain;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Knock.DatabaseTest.Catalog;

/// <summary>Spec 017, R-06: «¿existe una colonia con este nombre?» contra las semillas reales, sin distinguir mayúsculas ni acentos.</summary>
public sealed class CatalogColoniaNameTests
{
    [Theory]
    [InlineData("Centro", true)]
    [InlineData("centro", true)]
    [InlineData("  CENTRO  ", true)]
    [InlineData("el llano 1a sección", true)]
    [InlineData("El Llano 1a Seccion", true)]
    [InlineData("Tultengo", true)]
    [InlineData("Colonia Que No Existe", false)]
    [InlineData("Cent", false)]
    [InlineData("Centro Norte", false)]
    [InlineData("{\"$ne\":null}", false)]
    public async Task NameLookup_FindsTheSeededColoniasIgnoringCaseAndAccents_CA_017_07(string name, bool expected)
    {
        await using var database = await CatalogDatabase.CreateAsync(seeded: true);
        var store = database.Services.GetRequiredService<CatalogStore>();

        var exists = await store.ColoniaNameExistsAsync(SearchNameNormalizer.Normalize(name), CancellationToken.None);

        exists.Should().Be(expected);
    }
}
