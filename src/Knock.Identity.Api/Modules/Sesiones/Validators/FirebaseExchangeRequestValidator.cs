using FluentValidation;
using Knock.Identity.Api.Modules.Sesiones.Dtos;

namespace Knock.Identity.Api.Modules.Sesiones.Validators;

/// <summary>Forma del canje (spec 015, R-03). Los campos se nombran como en el JSON; ningún mensaje repite el valor enviado.</summary>
internal sealed class FirebaseExchangeRequestValidator : AbstractValidator<FirebaseExchangeRequest>
{
    public const int MaxTokenLength = 4096;
    public const int DeviceIdLength = 36;
    public const int MaxDeviceNameLength = 80;

    public FirebaseExchangeRequestValidator()
    {
        RuleFor(x => x.FirebaseIdToken)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Is required.")
            .MaximumLength(MaxTokenLength).WithMessage($"Must have at most {MaxTokenLength} characters.");
        RuleFor(x => x.DeviceId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Is required.")
            .Must(IsCanonicalUuid).WithMessage("Must be a UUID of 36 characters.");
        RuleFor(x => x.AceptaAviso)
            .Must(accepted => accepted == true).WithMessage("Must be true.");
        RuleFor(x => x.Dispositivo)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Is required.")
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Is required.")
            .MaximumLength(MaxDeviceNameLength).WithMessage($"Must have at most {MaxDeviceNameLength} characters.");
    }

    private static bool IsCanonicalUuid(string? value) =>
        value is { Length: DeviceIdLength } && Guid.TryParseExact(value, "D", out _);
}
