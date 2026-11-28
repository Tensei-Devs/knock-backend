using System.Reflection;
using AwesomeAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Knock.ArchitectureTest;

/// <summary>Spec 037 T-06: el despachador de avisos solo recibe el evento ya entregado; no lee colecciones ni módulos de otros dominios (CA-10).</summary>
public class Spec037ArquitecturaTests
{
    private const string _push = "Knock.Messaging.Api.Modules.Avisos.Push";

    private static Assembly Messaging => Assembly.Load("Knock.Messaging.Api");

    [Fact]
    public void El_Despachador_No_Depende_De_Mongo_Redis_Ni_De_Otros_Modulos_CA_037_10()
    {
        Messaging.GetTypes().Should().Contain(type => type.Namespace == _push && type.Name == "DespachadorDeAvisos", "la regla no debe pasar en vacío");

        var result = Types.InAssembly(Messaging).That().ResideInNamespace(_push).ShouldNot().HaveDependencyOnAny(
            "MongoDB.Driver",
            "StackExchange.Redis",
            "Knock.Messaging.Api.Modules.Conversaciones",
            "Knock.Messaging.Api.Modules.Notices",
            "Knock.Messaging.Api.Modules.Chat").GetResult();

        result.IsSuccessful.Should().BeTrue("el módulo no debe depender de ese código");
    }

    [Fact]
    public void El_Modulo_Avisos_No_Depende_De_Conversaciones_Ni_De_Notices_CA_037_10()
    {
        var result = Types.InAssembly(Messaging).That().ResideInNamespace("Knock.Messaging.Api.Modules.Avisos").ShouldNot().HaveDependencyOnAny(
            "Knock.Messaging.Api.Modules.Conversaciones",
            "Knock.Messaging.Api.Modules.Notices").GetResult();

        result.IsSuccessful.Should().BeTrue("el módulo no debe depender de ese código");
    }
}
