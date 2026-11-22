using FluentValidation;
using Knock.Contracts.Safety;
using Knock.Messaging.Api.Modules.Bloqueos.Dtos;

namespace Knock.Messaging.Api.Modules.Bloqueos.Validators;

/// <summary>Formato del evento de bloqueo (spec 043, R-23): tipo del catálogo, identificadores acotados y sin campos desconocidos. Nunca repite lo recibido.</summary>
internal sealed class EventoDeBloqueoValidator : AbstractValidator<EventoDeBloqueo>
{
    public const int MaxIdentifier = 128;

    public EventoDeBloqueoValidator()
    {
        RuleFor(request => request.Extra).Must(extra => extra is null or { Count: 0 }).WithMessage("Unknown fields are not allowed.");
        RuleFor(request => request.Tipo).Must(value => value is BlockCreated.EventType or BlockRemoved.EventType).WithMessage("Invalid type.");
        RuleFor(request => request.EventoId).NotEmpty().MaximumLength(MaxIdentifier).WithMessage("Invalid event id.");
        RuleFor(request => request.BloqueaId).NotEmpty().MaximumLength(MaxIdentifier).WithMessage("Invalid person.");
        RuleFor(request => request.BloqueadoId).NotEmpty().MaximumLength(MaxIdentifier).WithMessage("Invalid person.");
        RuleFor(request => request.OcurridoEn).NotNull().WithMessage("Required.");
    }
}
