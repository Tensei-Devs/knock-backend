using FluentValidation;
using Knock.Market.Api.Modules.Media.Domain;
using Knock.Market.Api.Modules.Media.Dtos;

namespace Knock.Market.Api.Modules.Media.Validators;

/// <summary>Validación del cuerpo de la subida pública (spec 032, R-04, CA-02): propósito público, tipo permitido y tamaño de 1 a 5 MB. Responde 422 sin repetir el valor.</summary>
internal sealed class PedirSubidaImagenRequestValidator : AbstractValidator<PedirSubidaImagenRequest>
{
    public PedirSubidaImagenRequestValidator()
    {
        RuleFor(x => x.Proposito).Cascade(CascadeMode.Stop).NotEmpty()
            .Must(p => PropositoImagenExtensions.Parse(p) is not null).WithMessage("Must be perfil or trabajo.")
            .OverridePropertyName("proposito");
        RuleFor(x => x.TipoContenido).Cascade(CascadeMode.Stop).NotEmpty()
            .Must(t => MediaRules.IsAllowedContentType(t!)).WithMessage("Must be image/jpeg, image/png or image/webp.")
            .OverridePropertyName("tipoContenido");
        RuleFor(x => x.TamanoBytes).Cascade(CascadeMode.Stop).NotNull()
            .Must(t => t is { } v && MediaRules.IsAllowedSize(v)).WithMessage("Must be between 1 and 5242880.")
            .OverridePropertyName("tamanoBytes");
    }
}
