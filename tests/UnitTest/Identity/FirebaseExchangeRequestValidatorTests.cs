using AwesomeAssertions;
using Knock.Identity.Api.Modules.Sesiones.Dtos;
using Knock.Identity.Api.Modules.Sesiones.Validators;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 015, R-03 y CA-03: forma del cuerpo del canje.</summary>
public class FirebaseExchangeRequestValidatorTests
{
    private const string _validDevice = "3f2b8c1e-5a7d-4e9b-8c3a-1d2e3f4a5b6c";
    private const string _validName = "Teléfono de prueba";

    private static readonly FirebaseExchangeRequestValidator _validator = new();

    private static IEnumerable<string> FieldsWithErrors(FirebaseExchangeRequest request) =>
        _validator.Validate(request).Errors.Select(e => e.PropertyName).Distinct();

    [Fact]
    public void ValidRequest_HasNoErrors_CA_015_03()
    {
        _validator.Validate(new FirebaseExchangeRequest(new string('a', 4096), _validDevice, true, new string('n', 80))).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingToken_FailsOnItsOwnField_CA_015_03(string? token)
    {
        FieldsWithErrors(new FirebaseExchangeRequest(token, _validDevice, true, _validName)).Should().Equal("FirebaseIdToken");
    }

    [Fact]
    public void TokenOver4096_FailsOnItsOwnField_CA_015_03()
    {
        FieldsWithErrors(new FirebaseExchangeRequest(new string('a', 4097), _validDevice, true, _validName)).Should().Equal("FirebaseIdToken");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-uuid")]
    [InlineData("3f2b8c1e5a7d4e9b8c3a1d2e3f4a5b6c")]
    [InlineData("{3f2b8c1e-5a7d-4e9b-8c3a-1d2e3f4a5b6c}")]
    [InlineData("3f2b8c1e-5a7d-4e9b-8c3a-1d2e3f4a5b6c ")]
    [InlineData("3f2b8c1e-5a7d-4e9b-8c3a-1d2e3f4a5b6z")]
    public void InvalidDeviceId_FailsOnItsOwnField_CA_015_03(string? deviceId)
    {
        FieldsWithErrors(new FirebaseExchangeRequest("token", deviceId, true, _validName)).Should().Equal("DeviceId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public void NoticeNotAccepted_FailsOnItsOwnField_CA_015_03(bool? accepted)
    {
        FieldsWithErrors(new FirebaseExchangeRequest("token", _validDevice, accepted, _validName)).Should().Equal("AceptaAviso");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingDeviceName_FailsOnItsOwnField_CA_015_03(string? name)
    {
        FieldsWithErrors(new FirebaseExchangeRequest("token", _validDevice, true, name)).Should().Equal("Dispositivo");
    }

    [Fact]
    public void DeviceNameOver80_FailsOnItsOwnField_CA_015_03()
    {
        FieldsWithErrors(new FirebaseExchangeRequest("token", _validDevice, true, new string('n', 81))).Should().Equal("Dispositivo");
    }

    [Fact]
    public void EveryFieldInvalid_ReportsEachOne_CA_015_03()
    {
        FieldsWithErrors(new FirebaseExchangeRequest(null, null, null, null)).Should().BeEquivalentTo("FirebaseIdToken", "DeviceId", "AceptaAviso", "Dispositivo");
    }
}
