using FluentValidation;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;

namespace Knock.Market.Api.Modules.Requests.Validators;

/// <summary>
/// Formato de <c>GET /agenda</c> (spec 050, R-02 y R-24): <c>desde</c> y <c>hasta</c> son días reales <c>yyyy-MM-dd</c>, el rango no se invierte ni pasa de 31 días y <c>limite</c> va de 1
/// a 50. Responde 422 con <c>errors.&lt;campo&gt;</c> sin repetir el valor.
/// </summary>
internal sealed class AgendaQueryValidator : AbstractValidator<AgendaQuery>
{
    public const string FormatReason = "The value is not valid.";

    public AgendaQueryValidator()
    {
        RuleFor(x => x.Desde).Must(value => VisitScheduleRules.TryParseDay(value, out _)).WithMessage(FormatReason).When(x => x.Desde is not null);
        RuleFor(x => x.Hasta).Must(value => VisitScheduleRules.TryParseDay(value, out _)).WithMessage(FormatReason).When(x => x.Hasta is not null);
        RuleFor(x => x.Hasta)
            .Must((query, value) => VisitScheduleRules.IsValidAgendaRange(Day(query.Desde), Day(value)))
            .WithMessage(FormatReason)
            .When(x => VisitScheduleRules.TryParseDay(x.Desde, out _) && VisitScheduleRules.TryParseDay(x.Hasta, out _));
        RuleFor(x => x.Limite).InclusiveBetween(1, VisitScheduleRules.MaxAgendaLimit).WithMessage(FormatReason);
    }

    private static DateOnly Day(string? value) => VisitScheduleRules.TryParseDay(value, out var day) ? day : default;
}

/// <summary>Formato de <c>POST /visitas/{id}/reprogramar</c> (spec 050, R-05): <c>fechaHora</c> es obligatoria. Que sea futura y dentro del rango lo decide el servicio con su reloj.</summary>
internal sealed class RescheduleBodyValidator : AbstractValidator<RescheduleBody>
{
    public const string RequiredReason = "Required.";

    public RescheduleBodyValidator() => RuleFor(x => x.FechaHora).NotNull().WithMessage(RequiredReason);
}

/// <summary>Formato de <c>POST /visitas/{id}/reprogramar/respuesta</c> (spec 050, R-08): <c>acepta</c> es obligatorio.</summary>
internal sealed class RescheduleReplyBodyValidator : AbstractValidator<RescheduleReplyBody>
{
    public const string RequiredReason = "Required.";

    public RescheduleReplyBodyValidator() => RuleFor(x => x.Acepta).NotNull().WithMessage(RequiredReason);
}

/// <summary>Formato de <c>POST /visitas/{id}/cancelar</c> (spec 050, R-26): <c>motivo</c> de 8 a 300 caracteres. Nunca repite el valor.</summary>
internal sealed class CancelVisitBodyValidator : AbstractValidator<CancelVisitBody>
{
    public const string RequiredReason = "Required.";
    public const string FormatReason = "The value is not valid.";

    public CancelVisitBodyValidator() =>
        RuleFor(x => x.Motivo).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(RequiredReason)
            .Must(VisitScheduleRules.IsValidReason).WithMessage(FormatReason);
}

/// <summary>
/// Formato de <c>POST /visitas/{id}/terminar</c> (spec 050, R-28): descripción de 8 a 500 caracteres, recomendación opcional de hasta 300 y de 1 a 5 identificadores de foto
/// distintos. Que las fotos existan y sean del técnico lo decide el servicio. Nunca repite el valor.
/// </summary>
internal sealed class FinishVisitBodyValidator : AbstractValidator<FinishVisitBody>
{
    public const string RequiredReason = "Required.";
    public const string FormatReason = "The value is not valid.";

    public FinishVisitBodyValidator()
    {
        RuleFor(x => x.Descripcion).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(RequiredReason)
            .Must(VisitCloseRules.IsValidDescription).WithMessage(FormatReason);
        RuleFor(x => x.Recomendacion).Must(VisitCloseRules.IsValidRecommendation).WithMessage(FormatReason);
        RuleFor(x => x.Fotos).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(RequiredReason)
            .Must(photos => photos!.Count is >= VisitCloseRules.MinPhotos and <= VisitCloseRules.MaxPhotos).WithMessage(FormatReason)
            .Must(photos => photos!.All(id => MongoDB.Bson.ObjectId.TryParse(id, out _))).WithMessage(FormatReason)
            .Must(photos => photos!.Select(id => id.ToUpperInvariant()).Distinct(StringComparer.Ordinal).Count() == photos!.Count).WithMessage(FormatReason);
    }
}

/// <summary>Formato de <c>POST /visitas/{id}/no-terminado</c> (spec 050, R-29): <c>texto</c> de 8 a 500 caracteres. Nunca repite el valor.</summary>
internal sealed class NotFinishedBodyValidator : AbstractValidator<NotFinishedBody>
{
    public const string RequiredReason = "Required.";
    public const string FormatReason = "The value is not valid.";

    public NotFinishedBodyValidator() =>
        RuleFor(x => x.Texto).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(RequiredReason)
            .Must(VisitCloseRules.IsValidReport).WithMessage(FormatReason);
}
