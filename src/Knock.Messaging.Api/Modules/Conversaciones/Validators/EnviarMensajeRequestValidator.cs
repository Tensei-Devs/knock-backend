using FluentValidation;
using Knock.Messaging.Api.Modules.Conversaciones.Domain;
using Knock.Messaging.Api.Modules.Conversaciones.Dtos;

namespace Knock.Messaging.Api.Modules.Conversaciones.Validators;

/// <summary>Valida el texto del mensaje (spec 030, R-10) con la misma regla que usa el dominio; el motivo nunca repite el texto enviado.</summary>
internal sealed class EnviarMensajeRequestValidator : AbstractValidator<EnviarMensajeRequest>
{
    public EnviarMensajeRequestValidator()
    {
        RuleFor(x => x.Texto).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Texto is required.")
            .Must(texto => ReglasDeMensaje.Limpiar(texto) is not null).When(x => !string.IsNullOrEmpty(x.Texto), ApplyConditionTo.CurrentValidator)
            .WithMessage("Texto must have 1 to 1000 characters and no control characters.");
    }
}
