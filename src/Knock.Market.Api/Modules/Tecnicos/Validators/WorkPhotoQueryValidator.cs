using System.Globalization;
using FluentValidation;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using MongoDB.Bson;

namespace Knock.Market.Api.Modules.Tecnicos.Validators;

/// <summary>TEC-07 (R-23 y R-25): <c>cursor</c> de 24 hexadecimales y <c>limite</c> entre 1 y 50; si no, 422 con <c>errors.&lt;campo&gt;</c> sin repetir el valor.</summary>
internal sealed class WorkPhotoQueryValidator : AbstractValidator<WorkPhotoQuery>
{
    public WorkPhotoQueryValidator()
    {
        const string invalid = "The value is not valid.";
        RuleFor(x => x.Cursor).Must(v => v is null || ObjectId.TryParse(v, out _)).WithMessage(invalid).WithName("cursor");
        RuleFor(x => x.Limite)
            .Must(v => v is null || int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 1 and <= WorkPhotoQuery.MaxLimit)
            .WithMessage(invalid).WithName("limite");
    }
}
