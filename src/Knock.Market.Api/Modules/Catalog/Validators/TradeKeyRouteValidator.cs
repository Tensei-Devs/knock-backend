using FluentValidation;
using Knock.Market.Api.Modules.Catalog.Domain;
using Knock.Market.Api.Modules.Catalog.Dtos;

namespace Knock.Market.Api.Modules.Catalog.Validators;

/// <summary>Clave de oficio de la ruta (spec 045, R-02 y R-10): minúsculas, dígitos, guion y guion bajo, de 1 a 16, sin espacios. Un valor inválido responde 422 con <c>errors.clave</c> sin repetirlo.</summary>
internal sealed class TradeKeyRouteValidator : AbstractValidator<TradeKeyRoute>
{
    public TradeKeyRouteValidator() =>
        RuleFor(route => route.Clave).Must(TradeRules.IsValidKey).WithMessage(AdminPageQueryValidator.FormatReason);
}
