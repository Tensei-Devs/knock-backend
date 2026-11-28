using FluentValidation;
using Knock.Identity.Api.Modules.Safety.Domain;
using Knock.Identity.Api.Modules.Safety.Dtos;

namespace Knock.Identity.Api.Modules.Safety.Validators;

/// <summary>Formato del cuerpo de <c>POST /suspension/apelacion</c> (spec 060, R-03). Un valor inválido responde 422 con <c>errors.texto</c> y nunca lo repite.</summary>
internal sealed class SuspensionAppealBodyValidator : AbstractValidator<SuspensionAppealBody>
{
    public const string RequiredReason = "Required.";
    public const string FormatReason = "The value is not valid.";

    public SuspensionAppealBodyValidator() =>
        RuleFor(x => x.Texto).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(RequiredReason)
            .Must(ReportRules.IsValidText).WithMessage(FormatReason);
}

/// <summary>Formato del cuerpo de <c>POST /admin/apelaciones/{id}/resolucion</c> (spec 060, §5): <c>decision</c> es <c>aceptar</c> o <c>rechazar</c>.</summary>
internal sealed class AppealResolutionBodyValidator : AbstractValidator<AppealResolutionBody>
{
    public const string RequiredReason = "Required.";
    public const string FormatReason = "The value is not valid.";

    public AppealResolutionBodyValidator() =>
        RuleFor(x => x.Decision).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(RequiredReason)
            .Must(AppealDecisions.IsKnown).WithMessage(FormatReason);
}
