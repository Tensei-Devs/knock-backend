using FluentValidation;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using Knock.Market.Api.Modules.Tecnicos.Services;
using MongoDB.Bson;

namespace Knock.Market.Api.Modules.Tecnicos.Validators;

/// <summary>TEC-14 (R-37): <c>filtro</c> es la palabra de todos los oficios, auto, agua o luz y <c>colonia</c>, si viene, son 24 hexadecimales; si no, 422 con <c>errors.&lt;campo&gt;</c> sin repetir el valor.</summary>
internal sealed class ExploreQueryValidator : AbstractValidator<ExploreQuery>
{
    public ExploreQueryValidator()
    {
        const string invalid = "The value is not valid.";
        RuleFor(x => x.Filtro).Must(TechnicianExploreService.IsKnownFilter).WithMessage(invalid).WithName("filtro");
        RuleFor(x => x.Colonia).Must(v => v is null || v.Length == 24 && ObjectId.TryParse(v, out _)).WithMessage(invalid).WithName("colonia");
    }
}
