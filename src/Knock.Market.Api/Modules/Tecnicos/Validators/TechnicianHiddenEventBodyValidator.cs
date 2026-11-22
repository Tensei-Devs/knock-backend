using FluentValidation;
using Knock.Market.Api.Modules.Tecnicos.Dtos;

namespace Knock.Market.Api.Modules.Tecnicos.Validators;

/// <summary>Formato del evento <c>TecnicoOcultado</c> (spec 053, R-20): tres identificadores obligatorios, acotados y sin espacios ni controles. Nunca repite lo recibido.</summary>
internal sealed class TechnicianHiddenEventBodyValidator : AbstractValidator<TechnicianHiddenEventBody>
{
    public const int MaxIdentifierLength = 128;
    public const string RequiredReason = "Required.";
    public const string FormatReason = "The value is not valid.";

    public TechnicianHiddenEventBodyValidator()
    {
        RuleFor(x => x.EventoId).Cascade(CascadeMode.Stop).NotEmpty().WithMessage(RequiredReason).Must(IsIdentifier).WithMessage(FormatReason);
        RuleFor(x => x.UsuarioId).Cascade(CascadeMode.Stop).NotEmpty().WithMessage(RequiredReason).Must(IsIdentifier).WithMessage(FormatReason);
        RuleFor(x => x.ReporteId).Cascade(CascadeMode.Stop).NotEmpty().WithMessage(RequiredReason).Must(IsIdentifier).WithMessage(FormatReason);
    }

    private static bool IsIdentifier(string? value) =>
        value is { Length: <= MaxIdentifierLength } && value.All(character => !char.IsControl(character) && !char.IsWhiteSpace(character));
}
