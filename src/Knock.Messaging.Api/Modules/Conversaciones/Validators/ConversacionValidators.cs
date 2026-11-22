using FluentValidation;
using Knock.Messaging.Api.Modules.Conversaciones.Dtos;
using MongoDB.Bson;

namespace Knock.Messaging.Api.Modules.Conversaciones.Validators;

/// <summary>Formato de <c>POST /conversaciones</c> (spec 030, R-04): <c>solicitudId</c> y, si viene, <c>tecnicoId</c> son identificadores de 24 caracteres. Un valor inválido responde 422 con <c>errors.&lt;campo&gt;</c> y nunca se repite.</summary>
internal sealed class CrearConversacionBodyValidator : AbstractValidator<CrearConversacionBody>
{
    public const string RequiredReason = "Required.";
    public const string FormatReason = "The value is not valid.";

    public CrearConversacionBodyValidator()
    {
        RuleFor(x => x.SolicitudId).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(RequiredReason)
            .Must(IsIdentifier).WithMessage(FormatReason);
        RuleFor(x => x.TecnicoId).Must(IsIdentifier).When(x => x.TecnicoId is not null).WithMessage(FormatReason);
    }

    private static bool IsIdentifier(string? value) => value is { Length: 24 } && ObjectId.TryParse(value, out _);
}

/// <summary>Formato de <c>POST /conversaciones/{id}/leido</c> (R-19): <c>hasta</c> es obligatorio. Una fecha futura no es un error: el servidor la acota al último mensaje.</summary>
internal sealed class LeidoBodyValidator : AbstractValidator<LeidoBody>
{
    public const string RequiredReason = "Required.";

    public LeidoBodyValidator() =>
        RuleFor(x => x.Hasta).NotNull().WithMessage(RequiredReason);
}

/// <summary>Formato de <c>GET /conversaciones</c> (R-17): <c>limit</c> de 1 a 20. Un cursor ilegible lo rechaza el servicio con 400.</summary>
internal sealed class ConversacionesQueryValidator : AbstractValidator<ConversacionesQuery>
{
    public const string FormatReason = "The value is not valid.";

    public ConversacionesQueryValidator() =>
        RuleFor(x => x.Limit).InclusiveBetween(1, ConversacionesQuery.MaxLimit).When(x => x.Limit is not null).WithMessage(FormatReason);
}

/// <summary>Formato de <c>GET /conversaciones/{id}/mensajes</c> (R-16): <c>limit</c> de 1 a 50.</summary>
internal sealed class MensajesQueryValidator : AbstractValidator<MensajesQuery>
{
    public const string FormatReason = "The value is not valid.";

    public MensajesQueryValidator() =>
        RuleFor(x => x.Limit).InclusiveBetween(1, MensajesQuery.MaxLimit).When(x => x.Limit is not null).WithMessage(FormatReason);
}
