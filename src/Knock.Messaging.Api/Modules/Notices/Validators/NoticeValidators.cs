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

/// <summary>Valida el identificador de la ruta de marcar como leído (R-06): un <c>ObjectId</c> de 24 hexadecimales.</summary>
internal sealed class MarkNoticeReadRequestValidator : AbstractValidator<MarkNoticeReadRequest>
{
    public MarkNoticeReadRequestValidator() =>
        RuleFor(r => r.Id).Must(NoticeCursor.IsObjectId).WithMessage("The identifier is not valid.");
}
