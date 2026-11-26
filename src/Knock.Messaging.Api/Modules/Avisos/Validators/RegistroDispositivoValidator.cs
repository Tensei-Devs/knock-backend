using FluentValidation;
using Knock.Messaging.Api.Modules.Avisos.Dtos;

namespace Knock.Messaging.Api.Modules.Avisos.Validators;

internal sealed class RegistroDispositivoValidator : AbstractValidator<RegistroDispositivo>
{
    public RegistroDispositivoValidator()
    {
        RuleFor(request => request.Identificador).NotEmpty().MaximumLength(4096)
            .Must(value => value is null || !value.Any(char.IsControl))
            .WithMessage("Invalid identifier.");
        RuleFor(request => request.Plataforma).Must(value => value is "android" or "ios")
            .WithMessage("Invalid platform.");
    }
}
