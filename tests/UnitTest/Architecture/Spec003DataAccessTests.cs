using System.Text.RegularExpressions;
using AwesomeAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Knock.UnitTest.Architecture;

/// <summary>
/// Spec 003, CA-02 (R-04): solo la capa <c>Data</c> de cada módulo usa MongoDB y Redis; el cliente compartido vive en <c>Hosting</c>.
/// La parte de MongoDB ya la prueban <see cref="MongoAccessTests"/> (regla y fixtures que fallan); aquí se agrega Redis.
/// </summary>
public sealed class Spec003DataAccessTests
{
    private const string _redisNamespace = "StackExchange.Redis";
    private const string _root = "Knock.Market.Api";

    [Fact]
    public void RedisClientOnlyInHostingAndModuleData_CA_02()
    {
        var moduleData = $"^{Regex.Escape($"{_root}.Modules")}[.][^.]+[.]Data([.]|$)";
        var hosting = $"^{Regex.Escape($"{_root}.Hosting")}([.]|$)";

        Types.InAssembly(ModuleCatalog.ApiAssembly)
            .That().ResideInNamespaceMatching(hosting)
            .And().HaveDependencyOnAny(_redisNamespace)
            .GetTypes()
            .Should().NotBeEmpty("el cliente de Redis vive en Hosting; si no, la regla pasaría en vacío");

        var result = Types.InAssembly(ModuleCatalog.ApiAssembly)
            .That().DoNotResideInNamespaceMatching(moduleData)
            .And().DoNotResideInNamespaceMatching(hosting)
            .ShouldNot().HaveDependencyOnAny(_redisNamespace)
            .GetResult();

        (result.FailingTypes ?? []).Select(type => type.FullName).Should().BeEmpty();
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void RedisRule_WithServiceUsingRedis_Fails_CA_02()
    {
        var result = Types.InAssembly(typeof(Spec003DataAccessTests).Assembly)
            .That().ResideInNamespace("Knock.UnitTest.Architecture.Fixtures.Redis.Violating.Modules.Alpha.Services")
            .ShouldNot().HaveDependencyOnAny(_redisNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeFalse();
        (result.FailingTypes ?? []).Select(type => type.Name).Should().Contain("AlphaServiceReadsRedis");
    }
}
