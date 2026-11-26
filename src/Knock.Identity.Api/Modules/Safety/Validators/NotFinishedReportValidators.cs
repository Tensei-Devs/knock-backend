using FluentValidation;
using Knock.Identity.Api.Modules.Safety.Dtos;
using Knock.Identity.Api.Modules.Safety.Services;
using MongoDB.Bson;

namespace Knock.Identity.Api.Modules.Safety.Validators;

/// <summary>Formato de <c>POST /api/v1/interno/reportes-no-terminados</c> (spec 053): el tipo es <c>ReporteNoTerminadoCreado</c>, la visita es un identificador de 24 hexadecimales, las personas son GUID y no hay campos de más; si no, 422 sin repetir el valor.</summary>
internal sealed class NotFinishedReportEventBodyValidator : AbstractValidator<NotFinishedReportEventBody>
{
    public const string FormatReason = "The value is not valid.";

    public NotFinishedReportEventBodyValidator()
    {
        RuleFor(x => x.EventoId).NotEmpty().MaximumLength(100).WithMessage(FormatReason);
        RuleFor(x => x.Tipo).Equal(NotFinishedReportConsumer.EventType).WithMessage(FormatReason);
        RuleFor(x => x.VisitaId).Must(value => value is { Length: 24 } && ObjectId.TryParse(value, out _)).WithMessage(FormatReason);
        RuleFor(x => x.ClienteId).Must(IsPublicId).WithMessage(FormatReason);
        RuleFor(x => x.TecnicoUsuarioId).Must(IsPublicId).WithMessage(FormatReason);
        RuleFor(x => x.Extra).Must(extra => extra is null || extra.Count == 0).WithMessage(FormatReason);
    }

    private static bool IsPublicId(string? value) => value is { Length: 36 } && Guid.TryParseExact(value, "D", out _);
}
