using FluentValidation;
using Knock.Contracts.Safety;
using Knock.Market.Api.Modules.Requests.Dtos;

namespace Knock.Market.Api.Modules.Requests.Validators;

/// <summary>Formato del evento de bloqueo (spec 043, R-23): tipo del catálogo, identificadores acotados y sin campos desconocidos. Nunca repite lo recibido.</summary>
internal sealed class BlockEventBodyValidator : AbstractValidator<BlockEventBody>
{
    public const int MaxIdentifier = 128;

    public BlockEventBodyValidator()
    {
        RuleFor(x => x.Extra).Must(extra => extra is null or { Count: 0 }).WithMessage("Unknown fields are not allowed.");
        RuleFor(x => x.Tipo).Must(value => value is BlockCreated.EventType or BlockRemoved.EventType).WithMessage("Invalid type.");
        RuleFor(x => x.EventoId).NotEmpty().MaximumLength(MaxIdentifier).WithMessage("Invalid event id.");
        RuleFor(x => x.BloqueaId).NotEmpty().MaximumLength(MaxIdentifier).WithMessage("Invalid person.");
        RuleFor(x => x.BloqueadoId).NotEmpty().MaximumLength(MaxIdentifier).WithMessage("Invalid person.");
        RuleFor(x => x.OcurridoEn).NotNull().WithMessage("Required.");
    }
}
