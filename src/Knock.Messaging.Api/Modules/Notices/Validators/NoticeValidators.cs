using FluentValidation;
using Knock.Messaging.Api.Modules.Notices.Domain;
using Knock.Messaging.Api.Modules.Notices.Dtos;

namespace Knock.Messaging.Api.Modules.Notices.Validators;

/// <summary>Valida la consulta de la lista (R-02, R-03). Los motivos nunca repiten el valor recibido.</summary>
internal sealed class ListNoticesQueryValidator : AbstractValidator<ListNoticesQuery>
{
    public ListNoticesQueryValidator()
    {
        RuleFor(q => q.Limite).Must(raw => NoticeRules.ParseLimit(raw) is not null).WithMessage("The limit must be a whole number from 1 to 50.");
        RuleFor(q => q.Leido).Must(raw => NoticeRules.ParseRead(raw).Valid).WithMessage("The value must be true or false.");
        RuleFor(q => q.ComoRol).Must(raw => string.IsNullOrEmpty(raw) || NoticeRules.IsKnownRole(raw)).WithMessage("The role is not one of the allowed values.");
        RuleFor(q => q.Cursor).Must(raw => string.IsNullOrEmpty(raw) || NoticeCursor.TryDecode(raw, out _)).WithMessage("The cursor is not valid.");
    }
}

/// <summary>
/// Valida el cuerpo del cambio de preferencias (PRE-02): lista no vacía de tipos conocidos sin repetir, cada uno con <c>activo</c>, y los avisos de seguridad no se pueden apagar (D-06).
/// Los motivos nunca repiten el valor recibido.
/// </summary>
internal sealed class UpdateNoticePreferencesRequestValidator : AbstractValidator<UpdateNoticePreferencesRequest>
{
    public UpdateNoticePreferencesRequestValidator()
    {
        RuleFor(r => r.Tipos).NotNull().WithMessage("The list of types is required.");
        RuleFor(r => r.Tipos!)
            .Must(list => list.Count >= 1 && list.Count <= NoticeRules.Types.Count).WithMessage("The list must have between one entry and one per type.")
            .Must(list => list.Select(c => c?.Tipo).Distinct(StringComparer.Ordinal).Count() == list.Count).WithMessage("A type cannot be repeated.")
            .When(r => r.Tipos is not null);
        RuleForEach(r => r.Tipos).NotNull().WithMessage("The entry is required.").SetValidator(new NoticePreferenceChangeValidator()).When(r => r.Tipos is not null);
    }
}

/// <summary>Valida un interruptor del cambio de preferencias: tipo conocido, valor presente y seguridad nunca apagada (D-06).</summary>
internal sealed class NoticePreferenceChangeValidator : AbstractValidator<NoticePreferenceChange>
{
    public NoticePreferenceChangeValidator()
    {
        RuleFor(c => c.Tipo).Must(NoticeRules.IsKnownType).WithMessage("The type is not one of the allowed values.");
        RuleFor(c => c.Activo).NotNull().WithMessage("The value is required.");
        RuleFor(c => c.Activo).Must(value => value is not false).WithMessage("Security notices cannot be turned off.").When(c => c.Tipo == NoticeRules.SecurityType);
    }
}

/// <summary>Valida el identificador de la ruta de marcar como leído (R-06): un <c>ObjectId</c> de 24 hexadecimales.</summary>
internal sealed class MarkNoticeReadRequestValidator : AbstractValidator<MarkNoticeReadRequest>
{
    public MarkNoticeReadRequestValidator() =>
        RuleFor(r => r.Id).Must(NoticeCursor.IsObjectId).WithMessage("The identifier is not valid.");
}
