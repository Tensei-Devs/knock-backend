using System.Reflection;
using AwesomeAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Knock.ArchitectureTest;

/// <summary>Fronteras entre servicios (spec 001, R-03 y R-04): ningún servicio conoce a otro y la biblioteca de contratos no conoce a ninguno.</summary>
public class ServiceBoundaryTests
{
    private static readonly string[] _services = ["Knock.Identity.Api", "Knock.Messaging.Api", "Knock.Market.Api"];

    public static TheoryData<string, string> ServicePairs
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var service in _services)
            {
                foreach (var other in _services.Where(o => o != service))
                {
                    data.Add(service, other);
                }
            }

            return data;
        }
    }

    public static TheoryData<string> ServiceNames
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var service in _services)
            {
                data.Add(service);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ServicePairs))]
    public void AService_DoesNotReferenceTheAssemblyOfAnotherService_CA_03(string service, string other)
    {
        var references = Assembly.Load(service).GetReferencedAssemblies().Select(a => a.Name);

        references.Should().NotContain(other);
    }

    [Theory]
    [MemberData(nameof(ServicePairs))]
    public void AService_DoesNotDependOnTheTypesOfAnotherService_CA_03(string service, string other)
    {
        var result = Types.InAssembly(Assembly.Load(service)).ShouldNot().HaveDependencyOnAny(other).GetResult();

        result.IsSuccessful.Should().BeTrue($"{service} no debe depender de {other}");
    }

    [Theory]
    [MemberData(nameof(ServiceNames))]
    public void Contracts_DoesNotReferenceAnyService_R_04(string service)
    {
        var references = Assembly.Load("Knock.Contracts").GetReferencedAssemblies().Select(a => a.Name);

        references.Should().NotContain(service);
    }

    [Theory]
    [MemberData(nameof(ServiceNames))]
    public void Gateway_DoesNotReferenceTheAssemblyOfAService_R_02(string service)
    {
        var references = Assembly.Load("Knock.Gateway").GetReferencedAssemblies().Select(a => a.Name);

        references.Should().NotContain(service);
    }

    [Fact]
    public void TheRuleCanFail_BecauseTheGatewayDoesDependOnContracts_CA_03()
    {
        // Caso negativo: la regla de dependencias detecta una dependencia que sí existe.
        var result = Types.InAssembly(Assembly.Load("Knock.Gateway")).ShouldNot().HaveDependencyOnAny("Knock.Contracts").GetResult();

        result.IsSuccessful.Should().BeFalse();
    }
}
