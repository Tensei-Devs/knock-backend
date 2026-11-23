extern alias messaging;
using AwesomeAssertions;
using Xunit;
using AvisoInterno = messaging::Knock.Messaging.Api.Modules.Avisos.Dtos.AvisoInterno;
using AvisoInternoValidator = messaging::Knock.Messaging.Api.Modules.Avisos.Validators.AvisoInternoValidator;

namespace Knock.UnitTest.Messaging;

public sealed class Spec037AvisoInternoValidatorTests
{
    private const string _resource = "507f1f77bcf86cd799439011";
    private readonly AvisoInternoValidator _validator = new();

    [Fact]
    public void ValidEvent_Passes_R_11()
    {
        _validator.Validate(new AvisoInterno("e1", "solicitud_nueva", "sub-1", "tecnico", _resource, null)).IsValid.Should().BeTrue();
        _validator.Validate(new AvisoInterno("e2", "sesion_revocada", null, null, null, "sid-1")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ReviewHiddenNoticeFromMarket_IsAcceptedOnlyForTheTechnicianWithoutFreeText_CA_061_09a()
    {
        _validator.Validate(new AvisoInterno("OpinionModerada:o:1", "opinion_oculta", "tech-user", "tecnico", _resource, null)).IsValid.Should().BeTrue();
        _validator.Validate(new AvisoInterno("OpinionModerada:o:1", "opinion_oculta", "tech-user", "cliente", _resource, null)).IsValid.Should().BeFalse();
        _validator.Validate(new AvisoInterno("OpinionModerada:o:1", "opinion_oculta", "tech-user", "tecnico", _resource, null)
        {
            Extra = new Dictionary<string, System.Text.Json.JsonElement> { ["motivo"] = System.Text.Json.JsonElement.Parse("\"texto\"") },
        }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("otro", "tecnico")]
    [InlineData("cotizacion_nueva", "tecnico")]
    [InlineData("solicitud_nueva", "cliente")]
    [InlineData("solicitud_nueva", null)]
    [InlineData("mensaje_nuevo", "cliente")]
    public void UnknownTypeOrMismatchedRole_Fails_CA_037_16(string tipo, string? rol)
    {
        _validator.Validate(new AvisoInterno("e1", tipo, "sub-1", rol, _resource, null)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void FreeTextField_Fails_CA_037_16()
    {
        var body = new AvisoInterno("e1", "solicitud_nueva", "sub-1", "tecnico", _resource, null)
        {
            Extra = new Dictionary<string, System.Text.Json.JsonElement> { ["texto"] = System.Text.Json.JsonElement.Parse("\"hola\"") },
        };

        _validator.Validate(body).IsValid.Should().BeFalse();
    }

    [Fact]
    public void SessionRevokedWithoutSid_Fails_R_13()
    {
        _validator.Validate(new AvisoInterno("e1", "sesion_revocada", null, null, null, null)).IsValid.Should().BeFalse();
    }
}
