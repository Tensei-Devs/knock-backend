using FluentValidation;
using Knock.Messaging.Api.Modules.Conversaciones.Domain;
using Knock.Messaging.Api.Modules.Conversaciones.Dtos;

namespace Knock.Messaging.Api.Modules.Conversaciones.Validators;

internal sealed class EventoInternoValidator : AbstractValidator<EventoInterno>
{
    private const int _maxIdentifier = 128;

    public EventoInternoValidator()
    {
        RuleFor(request => request.Extra).Must(extra => extra is null or { Count: 0 }).WithMessage("Unknown fields are not allowed.");
        RuleFor(request => request.EventoId).NotEmpty().MaximumLength(_maxIdentifier).WithMessage("Invalid event id.");
        RuleFor(request => request.Tipo).Must(EventosInternos.IsKnownType).WithMessage("Invalid type.");
        RuleFor(request => request.SolicitudId).NotEmpty().MaximumLength(_maxIdentifier).WithMessage("Invalid request id.");
        RuleFor(request => request.TecnicoUsuarioId).NotEmpty().MaximumLength(_maxIdentifier).WithMessage("Invalid technician.");
    }
}
