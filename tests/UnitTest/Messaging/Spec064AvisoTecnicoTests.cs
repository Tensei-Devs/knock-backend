extern alias messaging;
using AwesomeAssertions;
using Xunit;
using AvisoInterno = messaging::Knock.Messaging.Api.Modules.Avisos.Dtos.AvisoInterno;
using AvisoInternoValidator = messaging::Knock.Messaging.Api.Modules.Avisos.Validators.AvisoInternoValidator;
using AvisoRules = messaging::Knock.Messaging.Api.Modules.Avisos.Domain.AvisoRules;

namespace Knock.UnitTest.Messaging;

/// <summary>Spec 064 (R-07, R-08, D-01, CA-08): tipo, categoría, rol y texto fijo del aviso al técnico.</summary>
public sealed class Spec064AvisoTecnicoTests
{
    private const string _resource = "507f1f77bcf86cd799439011";

    [Fact]
    public void FixedText_IsExactlyTheOneDecidedByTheOwner_CA_064_08() =>
        AvisoRules.TechnicianAbsentText.Should().Be("El cliente reportó que no llegaste a tu visita. Si crees que es un error, contacta a soporte.");

    [Fact]
    public void FixedText_CarriesNoPersonalOrVisitData_CA_064_08()
    {
        AvisoRules.TechnicianAbsentText.Should().NotContainAny("$", "{", "}");
        var payload = AvisoRules.BuildPayload(AvisoRules.TechnicianAbsentEvent, _resource, null, Guid.NewGuid())!;
        payload["tipo"].Should().Be("visitas");
        payload["evento"].Should().Be("tecnico_no_se_presento");
        payload.Keys.Should().BeEquivalentTo("v", "tipo", "evento", "recurso", "aviso");
        payload.Values.Should().NotContain(value => value.Contains("llegaste", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("tecnico", true)]
    [InlineData("cliente", false)]
    [InlineData(null, false)]
    public void Role_IsOnlyTheTechnician_CA_064_08(string? rol, bool expected)
    {
        AvisoRules.RoleMatches("tecnico_no_se_presento", rol).Should().Be(expected);
        new AvisoInternoValidator().Validate(new AvisoInterno("TecnicoNoSePresento:v", "tecnico_no_se_presento", "tech-user", rol, _resource, null)).IsValid.Should().Be(expected);
    }
}
