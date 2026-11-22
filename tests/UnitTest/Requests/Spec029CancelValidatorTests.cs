using AwesomeAssertions;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;
using Knock.Market.Api.Modules.Requests.Validators;
using Xunit;

namespace Knock.UnitTest.Requests;

/// <summary>Spec 029, R-02 y R-13: formato del cuerpo de la cancelación y visibilidad de las solicitudes cerradas.</summary>
public sealed class Spec029CancelValidatorTests
{
    private readonly CancelRequestBodyValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("no_longer_needed")]
    [InlineData("solved_elsewhere")]
    [InlineData("takes_too_long")]
    [InlineData("other")]
    public void Reason_Is_Optional_Or_One_Of_The_Four_Values_CA_029_12(string? reason) =>
        _validator.Validate(new CancelRequestBody(reason)).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("OTHER")]
    [InlineData("inventado")]
    public void Unknown_Reason_Fails_On_The_Motivo_Field_And_Never_Echoes_It_CA_029_12(string reason)
    {
        var result = _validator.Validate(new CancelRequestBody(reason));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Motivo");
        result.Errors[0].ErrorMessage.Should().Be(CancelRequestBodyValidator.FormatReason);
    }

    [Theory]
    [InlineData("Cancelada", true)]
    [InlineData("Vencida", true)]
    [InlineData("Enviada", false)]
    [InlineData("Cotizando", false)]
    [InlineData("CitaConfirmada", false)]
    [InlineData("Terminada", false)]
    [InlineData("Calificada", false)]
    public void Only_Cancelled_And_Expired_Requests_Can_Be_Requested_Again_CA_029_10(string state, bool expected) =>
        TransicionesDeSolicitud.IsClosedWithoutAppointment(Enum.Parse<RequestState>(state)).Should().Be(expected);
}
