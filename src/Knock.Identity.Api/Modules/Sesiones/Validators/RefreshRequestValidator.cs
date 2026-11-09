using FluentValidation;
using Knock.Identity.Api.Modules.Sesiones.Dtos;

namespace Knock.Identity.Api.Modules.Sesiones.Validators;

/// <summary>Forma de la renovación (spec 016, R-01). Un token con la forma correcta pero desconocido responde 401, no 422; ningún mensaje repite el valor.</summary>
internal sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    /// <summary>32 bytes en Base64Url miden 43 caracteres; el tope deja margen sin aceptar cuerpos enormes.</summary>
    public const int MaxTokenLength = 128;

    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Is required.")
            .MaximumLength(MaxTokenLength).WithMessage($"Must have at most {MaxTokenLength} characters.");
    }
}
