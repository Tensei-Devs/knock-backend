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

/// <summary>Forma de <c>PUT /api/v1/admin/oficios/{clave}</c> (spec 045, R-10 y R-18): cada campo enviado debe ser válido y hay que enviar al menos uno. Que <c>clave</c> coincida con la ruta lo revisa el controlador.</summary>
internal sealed class UpdateTradeBodyValidator : AbstractValidator<UpdateTradeBody>
{
    public const string Reason = CreateTradeBodyValidator.Reason;

    public UpdateTradeBodyValidator()
    {
        RuleFor(x => x.Nombre).Must(TradeRules.IsValidName).When(x => x.Nombre is not null).WithMessage(Reason);
        RuleFor(x => x.Modalidad).Must(TradeRules.IsValidModality).When(x => x.Modalidad is not null).WithMessage(Reason);
        RuleFor(x => x.FrasesSugeridas).Must(TradeRules.IsValidPhrases).When(x => x.FrasesSugeridas is not null).WithMessage(Reason);
        RuleFor(x => x.TopePorcentaje).Must(TradeRules.IsValidPercentage).When(x => x.TopePorcentaje is not null).WithMessage(Reason);
        RuleFor(x => x.TopeFijoPorcentaje).Must(TradeRules.IsValidPercentage).When(x => x.TopeFijoPorcentaje is not null).WithMessage(Reason);
        RuleFor(x => x.PisoAprobacion).Must(TradeRules.IsPositive).When(x => x.PisoAprobacion is not null).WithMessage(Reason);
        RuleFor(x => x.VisitaMaxima).Must(TradeRules.IsPositive).When(x => x.VisitaMaxima is not null).WithMessage(Reason);
        RuleFor(x => x).Must(x => x.ChangedFields.Count > 0).OverridePropertyName("campos").WithMessage(Reason);
    }
}

/// <summary>Forma de <c>POST /api/v1/oficios-pedidos</c> (spec 045, R-13 y R-21): de 2 a 60 caracteres, sin datos de contacto. Un valor inválido responde 422 con <c>errors.texto</c> sin repetirlo.</summary>
internal sealed class TradeRequestBodyValidator : AbstractValidator<TradeRequestBody>
{
    public TradeRequestBodyValidator() =>
        RuleFor(x => x.Texto).Must(TradeRules.IsValidRequestText).WithMessage(CreateTradeBodyValidator.Reason);
}
