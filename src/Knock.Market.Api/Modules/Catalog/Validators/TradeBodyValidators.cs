using FluentValidation;
using Knock.Market.Api.Modules.Catalog.Domain;
using Knock.Market.Api.Modules.Catalog.Dtos;

namespace Knock.Market.Api.Modules.Catalog.Validators;

/// <summary>Forma de <c>POST /api/v1/admin/oficios</c> (spec 045, R-07 y R-18). Un valor inválido responde 422 con <c>errors.&lt;campo&gt;</c> y nunca se repite.</summary>
internal sealed class CreateTradeBodyValidator : AbstractValidator<CreateTradeBody>
{
    public const string Reason = AdminPageQueryValidator.FormatReason;

    public CreateTradeBodyValidator()
    {
        RuleFor(x => x.Clave).Must(TradeRules.IsValidKey).WithMessage(Reason);
        RuleFor(x => x.Nombre).Must(TradeRules.IsValidName).WithMessage(Reason);
        RuleFor(x => x.Modalidad).Must(TradeRules.IsValidModality).WithMessage(Reason);
        RuleFor(x => x.FrasesSugeridas).Must(TradeRules.IsValidPhrases).WithMessage(Reason);
        RuleFor(x => x.TopePorcentaje).Must(TradeRules.IsValidPercentage).WithMessage(Reason);
        RuleFor(x => x.TopeFijoPorcentaje).Must(TradeRules.IsValidPercentage).WithMessage(Reason);
        RuleFor(x => x.PisoAprobacion).Must(TradeRules.IsPositive).WithMessage(Reason);
        RuleFor(x => x.VisitaMaxima).Must(TradeRules.IsPositive).WithMessage(Reason);
    }
}
