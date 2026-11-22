using FluentValidation;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;

namespace Knock.Market.Api.Modules.Requests.Validators;

/// <summary>Formato de <c>POST /visitas/{id}/cotizacion-final</c> (spec 049, R-13): precio entero positivo y descripción de 8 a 500 caracteres. Nunca repite el valor.</summary>
internal sealed class FinalQuoteBodyValidator : AbstractValidator<FinalQuoteBody>
{
    public const string RequiredReason = "Required.";
    public const string FormatReason = "The value is not valid.";

    public FinalQuoteBodyValidator()
    {
        RuleFor(x => x.Precio).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(RequiredReason)
            .InclusiveBetween(FinalQuoteRules.MinPrice, FinalQuoteRules.MaxPrice).WithMessage(FormatReason);
        RuleFor(x => x.Descripcion).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(RequiredReason)
            .Must(value => value!.Trim().Length is >= FinalQuoteRules.MinDescription and <= FinalQuoteRules.MaxDescription).WithMessage(FormatReason);
    }
}

/// <summary>Formato de las decisiones (spec 049, R-05 y R-11): <c>decision</c> es <c>aprueba</c> o <c>rechaza</c>.</summary>
internal sealed class DecisionBodyValidator : AbstractValidator<DecisionBody>
{
    public const string RequiredReason = "Required.";
    public const string FormatReason = "The value is not valid.";

    public DecisionBodyValidator() =>
        RuleFor(x => x.Decision).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(RequiredReason)
            .Must(FinalQuoteRules.IsDecision).WithMessage(FormatReason);
}

/// <summary>Formato de <c>POST /cotizaciones/{id}/ajuste-precio</c> (spec 049, R-08): el mínimo sobre el tope se comprueba en el servicio, que conoce el tope.</summary>
internal sealed class AdjustmentBodyValidator : AbstractValidator<AdjustmentBody>
{
    public const string RequiredReason = "Required.";
    public const string FormatReason = "The value is not valid.";

    public AdjustmentBodyValidator()
    {
        RuleFor(x => x.MontoNuevo).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(RequiredReason)
            .InclusiveBetween(FinalQuoteRules.MinPrice, FinalQuoteRules.MaxPrice).WithMessage(FormatReason);
        RuleFor(x => x.Motivo).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(RequiredReason)
            .Must(value => value!.Trim().Length is >= FinalQuoteRules.MinReason and <= FinalQuoteRules.MaxReason).WithMessage(FormatReason);
    }
}
