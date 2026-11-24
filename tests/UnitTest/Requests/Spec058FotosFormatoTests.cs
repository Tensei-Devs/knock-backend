using AwesomeAssertions;
using FluentValidation.TestHelper;
using Knock.Market.Api.Modules.Requests.Dtos;
using Knock.Market.Api.Modules.Requests.Validators;
using Xunit;

namespace Knock.UnitTest.Requests;

/// <summary>Spec 058, T-08 (R-18, CA-23): el validador de <c>fotos</c> de la solicitud acepta el <c>archivoId</c> de 32 hexadecimales (UUID v7 de Media) y rechaza el de 24.</summary>
public sealed class Spec058FotosFormatoTests
{
    private static readonly CreateRequestBodyValidator _validator = new(new Clock());

    private static TestValidationResult<CreateRequestBody> Validate(string photo) =>
        _validator.TestValidate(new CreateRequestBody("plo", "Se rompió el tubo debajo del fregadero", [photo], "2026-11-14", "tarde", "aaaaaaaaaaaaaaaaaaaaaaaa", null));

    [Fact]
    public void Acepta_El_Id_De_32_Hexadecimales_Que_Emite_Media_CA_058_23()
    {
        Validate(Guid.CreateVersion7().ToString("N")).ShouldNotHaveValidationErrorFor(x => x.Fotos);
    }

    [Fact]
    public void Rechaza_El_Id_De_24_Hexadecimales_CA_058_23()
    {
        Validate("bbbbbbbbbbbbbbbbbbbbbbbb").ShouldHaveValidationErrorFor(x => x.Fotos).WithErrorMessage(CreateRequestBodyValidator.FormatReason);
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 13, 18, 0, 0, TimeSpan.Zero);
    }
}
