using FluentValidation;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Catalog;
using Knock.Market.Api.Modules.Requests.Dtos;

namespace Knock.Market.Api.Modules.Requests.Validators;

internal sealed class MarketReferenceQueryValidator : AbstractValidator<MarketReferenceQuery>
{
    private static readonly string[] _types = ["fijo", "rango", "visita"];

    public MarketReferenceQueryValidator(ICatalogApi catalog)
    {
        RuleFor(x => x.Oficio).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ValidationFilter.GenericReason)
            .Must(value => value!.Length <= 16).WithMessage(ValidationFilter.GenericReason)
            .MustAsync(async (value, token) => await catalog.FindActiveTradeAsync(value!, token) is not null).WithMessage(ValidationFilter.GenericReason);
        RuleFor(x => x.TipoTrabajo).Must(x => x is null || _types.Contains(x, StringComparer.Ordinal)).WithMessage(ValidationFilter.GenericReason);
    }
}
