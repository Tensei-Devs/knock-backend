using System.Globalization;
using FluentValidation;
using Knock.Market.Api.Modules.Tecnicos.Dtos;

namespace Knock.Market.Api.Modules.Tecnicos.Validators;

/// <summary>TEC-04 (R-12 y R-40): los cuatro límites son obligatorios y válidos (norte sobre sur, este sobre oeste) y el zoom va de 1 a 20; si no, 422 con
/// <c>errors.&lt;campo&gt;</c> sin repetir el valor.</summary>
internal sealed class MapQueryValidator : AbstractValidator<MapQuery>
{
    public MapQueryValidator()
    {
        const string invalid = "The value is not valid.";
        RuleFor(x => x.Norte).Must(v => Coordinate(v, 90)).WithMessage(invalid).WithName("norte");
        RuleFor(x => x.Sur).Must(v => Coordinate(v, 90)).WithMessage(invalid).WithName("sur");
        RuleFor(x => x.Este).Must(v => Coordinate(v, 180)).WithMessage(invalid).WithName("este");
        RuleFor(x => x.Oeste).Must(v => Coordinate(v, 180)).WithMessage(invalid).WithName("oeste");
        RuleFor(x => x.Sur).Must((x, v) => Number(v) < Number(x.Norte)).When(x => Coordinate(x.Norte, 90) && Coordinate(x.Sur, 90)).WithMessage(invalid).WithName("sur");
        RuleFor(x => x.Oeste).Must((x, v) => Number(v) < Number(x.Este)).When(x => Coordinate(x.Este, 180) && Coordinate(x.Oeste, 180)).WithMessage(invalid).WithName("oeste");
        RuleFor(x => x.Zoom).Must(v => v is null || int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 1 and <= 20)
            .WithMessage(invalid).WithName("zoom");
        RuleFor(x => x.Oficio).Must(v => v is null || v.Length is >= 1 and <= 16 && v.All(char.IsAsciiLetterOrDigit)).WithMessage(invalid).WithName("oficio");
    }

    private static double Number(string? value) => double.Parse(value!, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static bool Coordinate(string? value, double limit) =>
        value is not null && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && Math.Abs(parsed) <= limit;
}
