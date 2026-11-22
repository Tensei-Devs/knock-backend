using System.Text.RegularExpressions;
using FluentValidation;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;

namespace Knock.Market.Api.Modules.Requests.Validators;

/// <summary>Formato del evento de cuenta (spec 059, §5): <c>tipo</c> del catálogo cerrado, <c>eventoId</c> acotado y <c>usuarioId</c> sin espacios ni controles. Nunca repite lo recibido.</summary>
internal sealed partial class AccountEventBodyValidator : AbstractValidator<AccountEventBody>
{
    public const string RequiredReason = "Required.";
    public const string FormatReason = "The value is not valid.";

    public AccountEventBodyValidator()
    {
        RuleFor(x => x.EventoId).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(RequiredReason)
            .Must(value => EventIdPattern().IsMatch(value!)).WithMessage(FormatReason);
        RuleFor(x => x.Tipo).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(RequiredReason)
            .Must(AccountEventRules.IsKnown).WithMessage(FormatReason);
        RuleFor(x => x.UsuarioId).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(RequiredReason)
            .Must(value => value!.Length <= AccountEventRules.MaxUserIdLength && value.All(character => !char.IsControl(character) && !char.IsWhiteSpace(character))).WithMessage(FormatReason);
    }

    [GeneratedRegex("^[A-Za-z0-9:_.-]{1,128}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex EventIdPattern();
}
