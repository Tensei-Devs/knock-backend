using AwesomeAssertions;
using Knock.Market.Api.Modules.Moderation.Services;
using NetArchTest.Rules;
using Xunit;

namespace Knock.UnitTest.Moderation;

public sealed class Spec031ArquitecturaTests
{
    [Fact]
    public void Un_Solo_Punto_De_Acceso_CA_031_17()
    {
        var assembly = typeof(ListaLocal).Assembly;
        var conjunto = Types.InAssembly(assembly).That().ResideInNamespace("Knock.Market.Api").GetTypes().ToList();
        conjunto.Should().NotBeEmpty();
        var result = Types.InAssembly(assembly).That().DoNotResideInNamespace("Knock.Market.Api.Modules.Moderation").And().DoNotResideInNamespace("Knock.UnitTest")
            .ShouldNot().HaveDependencyOnAny("Knock.Market.Api.Modules.Moderation.Services.ContentSafetyClient", "Knock.Market.Api.Modules.Moderation.Services.IContentSafetyClient").GetResult();
        result.FailingTypes.Should().BeNullOrEmpty();
    }
}
