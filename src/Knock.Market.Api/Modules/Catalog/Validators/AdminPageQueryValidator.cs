using FluentValidation;
using Knock.Market.Api.Modules.Catalog.Dtos;

namespace Knock.Market.Api.Modules.Catalog.Validators;

/// <summary>Formato de las listas del panel (spec 045, R-15 y R-17): <c>limite</c> de 1 a 50; otro valor responde 422 con <c>errors.Limite</c> sin repetirlo. Un cursor ilegible lo rechaza el servicio con 400.</summary>
internal sealed class AdminPageQueryValidator : AbstractValidator<AdminPageQuery>
{
    public const string FormatReason = "The value is not valid.";

    public AdminPageQueryValidator() =>
        RuleFor(x => x.Limite).InclusiveBetween(1, AdminPageQuery.MaxLimit).WithMessage(FormatReason);
}
