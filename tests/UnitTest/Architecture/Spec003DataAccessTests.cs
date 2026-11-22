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
/// y los demás servicios: Identity (MongoDB y Redis), Messaging (MongoDB y, desde BUG-004, Redis para leer las sesiones canceladas) y el Gateway (solo Redis).
/// </summary>
public sealed class Spec003DataAccessTests
{
    private const string _redisNamespace = "StackExchange.Redis";
    private const string _mongoNamespace = "MongoDB.Driver";
    private const string _root = "Knock.Market.Api";

    private static readonly Dictionary<string, (Assembly Assembly, bool Mongo, bool Redis)> _services = new()
    {
        ["Knock.Identity.Api"] = (typeof(identity::Knock.Identity.Api.Hosting.DataExtensions).Assembly, true, true),
        ["Knock.Messaging.Api"] = (typeof(messaging::Knock.Messaging.Api.Hosting.DataExtensions).Assembly, true, true),
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
            // El cliente de Redis vive en Knock.Hosting (spec 055): en el servicio solo importa que no lo use quien no debe.
            if (client == _mongoNamespace || !expected)
            {
                Types.InAssembly(assembly).That().ResideInNamespaceMatching(hosting).And().HaveDependencyOnAny(client).GetTypes()
                    .Any().Should().Be(expected, $"{service} {(expected ? "usa" : "no usa")} {client} (decisión del líder, 2026-11-04)");
            }

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
        Types.InAssembly(typeof(Knock.Hosting.Redis.RedisExtensions).Assembly)
            .That().HaveDependencyOnAny(_redisNamespace)
            .GetTypes()
            .Should().NotBeEmpty("el cliente de Redis vive en Knock.Hosting (spec 055); si no, la regla pasaría en vacío");

        var result = RedisAccessRule.Evaluate(ModuleCatalog.ApiAssembly, _root);

        result.FailingTypes.Should().BeEmpty();
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void RedisRule_WithServiceUsingRedis_Fails_CA_003_02()
    {
        var result = RedisAccessRule.Evaluate(typeof(Spec003DataAccessTests).Assembly, "Knock.UnitTest.Architecture.Fixtures.Redis.Violating");

        result.IsSuccessful.Should().BeFalse();
        result.FailingTypes.Should().Contain(name => name.EndsWith(".AlphaServiceReadsRedis", StringComparison.Ordinal));
    }
}
