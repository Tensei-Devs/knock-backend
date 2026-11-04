extern alias gateway;
extern alias identity;
extern alias messaging;

using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Knock.UnitTest.Architecture;

/// <summary>
/// Spec 003, CA-02 (R-04): solo la capa <c>Data</c> de cada módulo usa MongoDB y Redis; el cliente compartido vive en <c>Hosting</c>.
/// En Market, la parte de MongoDB ya la prueban <see cref="MongoAccessTests"/> (regla y fixtures que fallan); aquí se agrega Redis
/// y los demás servicios: Identity (MongoDB y Redis), Messaging (solo MongoDB) y el Gateway (solo Redis).
/// </summary>
public sealed class Spec003DataAccessTests
{
    private const string _redisNamespace = "StackExchange.Redis";
    private const string _mongoNamespace = "MongoDB.Driver";
    private const string _root = "Knock.Market.Api";

    private static readonly Dictionary<string, (Assembly Assembly, bool Mongo, bool Redis)> _services = new()
    {
        ["Knock.Identity.Api"] = (typeof(identity::Knock.Identity.Api.Hosting.DataExtensions).Assembly, true, true),
        ["Knock.Messaging.Api"] = (typeof(messaging::Knock.Messaging.Api.Hosting.DataExtensions).Assembly, true, false),
        ["Knock.Gateway"] = (typeof(gateway::Knock.Gateway.Hosting.DataExtensions).Assembly, false, true),
    };

    public static TheoryData<string> Services() => [.. _services.Keys];

    [Theory]
    [MemberData(nameof(Services))]
    public void ServiceUsesOnlyItsDataClients_AndOnlyInHostingOrModuleData_CA_003_02(string service)
    {
        var (assembly, usesMongo, usesRedis) = _services[service];
        var moduleData = $"^{Regex.Escape($"{service}.Modules")}[.][^.]+[.]Data([.]|$)";
        var hosting = $"^{Regex.Escape($"{service}.Hosting")}([.]|$)";

        foreach (var (client, expected) in new[] { (_mongoNamespace, usesMongo), (_redisNamespace, usesRedis) })
        {
            Types.InAssembly(assembly).That().ResideInNamespaceMatching(hosting).And().HaveDependencyOnAny(client).GetTypes()
                .Any().Should().Be(expected, $"{service} {(expected ? "usa" : "no usa")} {client} (decisión del líder, 2026-11-04)");
            var result = Types.InAssembly(assembly)
                .That().DoNotResideInNamespaceMatching(moduleData)
                .And().DoNotResideInNamespaceMatching(hosting)
                .ShouldNot().HaveDependencyOnAny(client)
                .GetResult();
            (result.FailingTypes ?? []).Select(type => type.FullName).Should().BeEmpty();
        }
    }

    [Fact]
    public void RedisClientOnlyInHostingAndModuleData_CA_003_02()
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
    public void RedisRule_WithServiceUsingRedis_Fails_CA_003_02()
    {
        var result = Types.InAssembly(typeof(Spec003DataAccessTests).Assembly)
            .That().ResideInNamespace("Knock.UnitTest.Architecture.Fixtures.Redis.Violating.Modules.Alpha.Services")
            .ShouldNot().HaveDependencyOnAny(_redisNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeFalse();
        (result.FailingTypes ?? []).Select(type => type.Name).Should().Contain("AlphaServiceReadsRedis");
    }
}
