using System.Text.RegularExpressions;
using FluentValidation;
using Knock.Market.Api.Modules.Catalog.Dtos;
using Knock.Market.Api.Modules.Catalog.ModelBinders;

namespace Knock.Market.Api.Modules.Catalog.Validators;

/// <summary>El mismo formato de nombre de colonia que valida Identidad en el perfil. Los motivos nunca incluyen el valor recibido.</summary>
internal sealed partial class NeighborhoodExistsQueryValidator : AbstractValidator<NeighborhoodExistsQuery>
{
    public const string RequiredReason = "Required.";
    public const string FormatReason = "The value is not valid.";

    public NeighborhoodExistsQueryValidator()
    {
        RuleFor(x => x.Nombre).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(RequiredReason)
            .Must(value => value is { Length: > 0 and <= NeighborhoodExistsQuery.MaxLength } && value != SingleValueQueryModelBinder.RepeatedValue && NamePattern().IsMatch(value))
            .WithMessage(FormatReason);
    }

    [GeneratedRegex(@"^[\p{L}\p{M}\p{N}][\p{L}\p{M}\p{N} .,'’()/#-]*\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex NamePattern();
}
