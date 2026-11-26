using FluentValidation;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;

namespace Knock.Market.Api.Modules.Requests.Validators;

/// <summary>Formato de <c>POST /solicitudes/{id}/cancelar</c> (spec 029, R-02): <c>motivo</c> es opcional y, si viene, uno de los cuatro valores; otro responde 422 con <c>errors.motivo</c>. Nunca repite el valor.</summary>
internal sealed class CancelRequestBodyValidator : AbstractValidator<CancelRequestBody>
{
    public const string FormatReason = "The value is not valid.";

    public CancelRequestBodyValidator() =>
        RuleFor(x => x.Motivo)
            .Must(value => TransicionesDeSolicitud.CancellationReasons.Contains(value!, StringComparer.Ordinal))
            .WithMessage(FormatReason)
            .When(x => x.Motivo is not null);
}
