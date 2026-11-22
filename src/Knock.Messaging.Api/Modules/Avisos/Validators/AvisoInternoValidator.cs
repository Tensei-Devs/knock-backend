using FluentValidation;
using Knock.Messaging.Api.Modules.Avisos.Domain;
using Knock.Messaging.Api.Modules.Avisos.Dtos;

namespace Knock.Messaging.Api.Modules.Avisos.Validators;

internal sealed class AvisoInternoValidator : AbstractValidator<AvisoInterno>
{
    private const int _maxIdentifier = 128;

    public AvisoInternoValidator()
    {
        RuleFor(request => request.Extra).Must(extra => extra is null or { Count: 0 }).WithMessage("Unknown fields are not allowed.");
        RuleFor(request => request.EventoId).NotEmpty().MaximumLength(_maxIdentifier).Must(value => value is null || !value.Contains('|', StringComparison.Ordinal))
            .WithMessage("Invalid event id.");
        RuleFor(request => request.Tipo).Must(value => AvisoRules.IsCatalogType(value) && value != AvisoRules.NewMessageEvent).WithMessage("Invalid type.");
        When(request => request.Tipo == AvisoRules.SessionRevokedEvent, () =>
            RuleFor(request => request.Sid).NotEmpty().MaximumLength(_maxIdentifier).WithMessage("Invalid session."));
        When(request => request.Tipo != AvisoRules.SessionRevokedEvent && AvisoRules.IsCatalogType(request.Tipo), () =>
        {
            RuleFor(request => request.DestinatarioId).NotEmpty().MaximumLength(_maxIdentifier).WithMessage("Invalid recipient.");
            RuleFor(request => request.Rol).Must((request, role) => AvisoRules.RoleMatches(request.Tipo!, role)).WithMessage("Invalid role for the type.");
            RuleFor(request => request.Recurso).Must(AvisoRules.IsValidResource).WithMessage("Invalid resource.");
        });
    }
}
