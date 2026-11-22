using System.Text.RegularExpressions;
using FluentValidation;
using Knock.Market.Api.Modules.Assistant.Dtos;

namespace Knock.Market.Api.Modules.Assistant.Validators;

/// <summary>Reglas de forma de los cuerpos y consultas del asistente (spec 052, R-02, R-04, R-06, R-08, R-11, R-14, R-19 y R-21). Nunca repiten el valor enviado.</summary>
internal static partial class AsistenteRules
{
    public const string Invalid = "The value is not valid.";

    public static bool IsObjectId(string? value) => value is not null && ObjectIdText().IsMatch(value);

    public static bool IsPlain(string? value, int min, int max) => value is not null && value.Trim().Length >= min && value.Length <= max && !value.Any(char.IsControl);

    public static bool IsPlainMessage(string? value, int min, int max) =>
        value is not null && value.Trim().Length >= min && value.Length <= max && !value.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'));

    public static bool IsList(IReadOnlyList<string>? list) =>
        list is { Count: <= BitacoraBody.MaxItems } && list.All(item => item is not null && item.Trim().Length >= 1 && item.Length <= BitacoraBody.MaxItemLength && !item.Any(char.IsControl));

    public static bool IsSlug(string? value, int max) => value is not null && Slug().IsMatch(value) && value.Length <= max;

    [GeneratedRegex(@"\A[0-9a-fA-F]{24}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex ObjectIdText();

    [GeneratedRegex(@"\A[\p{L}\p{N}][\p{L}\p{N} _\-]*\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex Slug();
}

internal sealed class TurnoBodyValidator : AbstractValidator<TurnoBody>
{
    public TurnoBodyValidator()
    {
        RuleFor(x => x.Mensaje).Must(v => AsistenteRules.IsPlainMessage(v, 1, TurnoBody.MaxMessage)).WithMessage(AsistenteRules.Invalid);
        RuleFor(x => x.Foto).Must(v => v is null || Guid.TryParse(v, out _)).WithMessage(AsistenteRules.Invalid);
        RuleFor(x => x.Contexto).Must(v => v is null || AsistenteRules.IsPlain(v, 0, TurnoBody.MaxContext)).WithMessage(AsistenteRules.Invalid);
    }
}

internal sealed class ConversacionBodyValidator : AbstractValidator<ConversacionBody>
{
    public ConversacionBodyValidator() => RuleFor(x => x.ConversacionId).Must(v => v is not null && AsistenteRules.IsObjectId(v)).WithMessage(AsistenteRules.Invalid);
}

internal sealed class RangoPrecioQueryValidator : AbstractValidator<RangoPrecioQuery>
{
    public RangoPrecioQueryValidator()
    {
        RuleFor(x => x.Oficio).Must(v => AsistenteRules.IsSlug(v, 60)).WithMessage(AsistenteRules.Invalid);
        RuleFor(x => x.TipoTrabajo).Must(v => AsistenteRules.IsSlug(v, 80)).WithMessage(AsistenteRules.Invalid);
    }
}

internal sealed class BitacoraBodyValidator : AbstractValidator<BitacoraBody>
{
    public BitacoraBodyValidator()
    {
        RuleFor(x => x.Sabe).Must(AsistenteRules.IsList).WithMessage(AsistenteRules.Invalid);
        RuleFor(x => x.PorConfirmar).Must(AsistenteRules.IsList).WithMessage(AsistenteRules.Invalid);
        RuleFor(x => x.QueLlevar).Must(AsistenteRules.IsList).WithMessage(AsistenteRules.Invalid);
    }
}

internal sealed class SolicitudRouteValidator : AbstractValidator<SolicitudRoute>
{
    public SolicitudRouteValidator() => RuleFor(x => x.SolicitudId).Must(AsistenteRules.IsObjectId).WithMessage(AsistenteRules.Invalid);
}
