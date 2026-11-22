using FluentValidation;
using Knock.Identity.Api.Modules.Moderation.Domain;
using Knock.Identity.Api.Modules.Moderation.Dtos;
using MongoDB.Bson;

namespace Knock.Identity.Api.Modules.Moderation.Validators;

/// <summary>Formato del identificador de un caso: un <c>ObjectId</c> de 24 caracteres (R-05).</summary>
internal sealed class ReviewRouteValidator : AbstractValidator<ReviewRoute>
{
    public const string FormatReason = "The value is not valid.";

    public ReviewRouteValidator() =>
        RuleFor(x => x.Id).Must(value => value is { Length: 24 } && ObjectId.TryParse(value, out _)).WithMessage(FormatReason);
}

/// <summary>Formato de <c>GET /admin/verificaciones</c> (R-02). Un valor inválido responde 422 con <c>errors.&lt;campo&gt;</c> y nunca se repite.</summary>
internal sealed class VerificationQueueQueryValidator : AbstractValidator<VerificationQueueQuery>
{
    public const string FormatReason = "The value is not valid.";

    public VerificationQueueQueryValidator()
    {
        RuleFor(x => x.Tipo).Must(ReviewKinds.IsKnown).WithMessage(FormatReason).When(x => x.Tipo is not null);
        RuleFor(x => x.Estado).Must(ReviewStates.IsVerificationState).WithMessage(FormatReason).When(x => x.Estado is not null);
        RuleFor(x => x.Limite).InclusiveBetween(1, ReviewRules.MaxLimit).WithMessage(FormatReason);
        RuleFor(x => x.Hasta).GreaterThanOrEqualTo(x => x.Desde!.Value).WithMessage(FormatReason).When(x => x.Desde is not null && x.Hasta is not null);
    }
}

/// <summary>Formato del cuerpo de la decisión de una verificación (R-08 y R-18): el motivo es obligatorio al rechazar y mide de 8 a 300 caracteres.</summary>
internal sealed class VerificationDecisionBodyValidator : AbstractValidator<VerificationDecisionBody>
{
    public const string RequiredReason = "Required.";
    public const string FormatReason = "The value is not valid.";

    public VerificationDecisionBodyValidator()
    {
        RuleFor(x => x.Decision).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(RequiredReason)
            .Must(ReviewDecisions.IsKnown).WithMessage(FormatReason);
        RuleFor(x => x.Motivo).NotEmpty().WithMessage(RequiredReason).When(x => x.Decision == ReviewDecisions.Rejected);
        RuleFor(x => x.Motivo).Must(ReviewRules.IsValidReason).WithMessage(FormatReason).When(x => !string.IsNullOrEmpty(x.Motivo));
    }
}

/// <summary>Formato de <c>GET /admin/reportes</c> (R-11).</summary>
internal sealed class ReportQueueQueryValidator : AbstractValidator<ReportQueueQuery>
{
    public const string FormatReason = "The value is not valid.";

    public ReportQueueQueryValidator()
    {
        RuleFor(x => x.Gravedad).Must(ReportSeverities.IsKnown).WithMessage(FormatReason).When(x => x.Gravedad is not null);
        RuleFor(x => x.Estado).Must(ReviewStates.IsReportState).WithMessage(FormatReason).When(x => x.Estado is not null);
        RuleFor(x => x.Limite).InclusiveBetween(1, ReviewRules.MaxLimit).WithMessage(FormatReason);
        RuleFor(x => x.Hasta).GreaterThanOrEqualTo(x => x.Desde!.Value).WithMessage(FormatReason).When(x => x.Desde is not null && x.Hasta is not null);
    }
}

/// <summary>Formato del cuerpo de la decisión de un reporte (R-14 y R-20): gravedad de la lista cerrada y motivo de 8 a 300 caracteres.</summary>
internal sealed class ReportDecisionBodyValidator : AbstractValidator<ReportDecisionBody>
{
    public const string RequiredReason = "Required.";
    public const string FormatReason = "The value is not valid.";

    public ReportDecisionBodyValidator()
    {
        RuleFor(x => x.Gravedad).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(RequiredReason)
            .Must(ReportSeverities.IsKnown).WithMessage(FormatReason);
        RuleFor(x => x.Motivo).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(RequiredReason)
            .Must(ReviewRules.IsValidReason).WithMessage(FormatReason);
    }
}
