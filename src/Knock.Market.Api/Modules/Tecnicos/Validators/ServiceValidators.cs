using FluentValidation;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using MongoDB.Bson;

namespace Knock.Market.Api.Modules.Tecnicos.Validators;

/// <summary>Reglas de forma de los campos de un servicio (spec 047, R-13), compartidas por el alta y la edición.</summary>
internal static class ServiceRules
{
    public const string Invalid = "The value is not valid.";
    public const int MaxName = 60;
    public const int MaxDetail = 150;
    public const int MaxTradeKey = 16;

    public static bool ValidName(string? value) => value is null || (value.Trim().Length is >= 1 and <= MaxName && !HasControl(value));

    public static bool ValidDetail(string? value) => value is null || (value.Length <= MaxDetail && !HasControl(value));

    public static bool ValidPrice(int? value) => value is null or > 0;

    public static bool ValidTrade(string? value) =>
        value is null || (value.Length is >= 1 and <= MaxTradeKey && value.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'));

    public static bool ValidObjectId(string? value) => value is { Length: 24 } && ObjectId.TryParse(value, out _);

    private static bool HasControl(string value) => value.Any(char.IsControl);
}

/// <summary>SRV-02 (R-06): nombre, precio y oficio obligatorios; detalle opcional.</summary>
internal sealed class CreateServiceBodyValidator : AbstractValidator<CreateServiceBody>
{
    public CreateServiceBodyValidator()
    {
        RuleFor(x => x.Nombre).NotNull().Must(ServiceRules.ValidName).WithMessage(ServiceRules.Invalid);
        RuleFor(x => x.Detalle).Must(ServiceRules.ValidDetail).WithMessage(ServiceRules.Invalid);
        RuleFor(x => x.PrecioReferencia).NotNull().Must(ServiceRules.ValidPrice).WithMessage(ServiceRules.Invalid);
        RuleFor(x => x.Oficio).NotNull().Must(ServiceRules.ValidTrade).WithMessage(ServiceRules.Invalid);
    }
}

/// <summary>SRV-03 (R-08 y R-13): cada campo enviado cumple su regla; uno ausente no cambia.</summary>
internal sealed class UpdateServiceBodyValidator : AbstractValidator<UpdateServiceBody>
{
    public UpdateServiceBodyValidator()
    {
        RuleFor(x => x.Nombre).Must(ServiceRules.ValidName).WithMessage(ServiceRules.Invalid);
        RuleFor(x => x.Detalle).Must(ServiceRules.ValidDetail).WithMessage(ServiceRules.Invalid);
        RuleFor(x => x.PrecioReferencia).Must(ServiceRules.ValidPrice).WithMessage(ServiceRules.Invalid);
        RuleFor(x => x.Oficio).Must(ServiceRules.ValidTrade).WithMessage(ServiceRules.Invalid);
    }
}

/// <summary>Ruta de SRV-03 y SRV-04 (R-11): <c>id</c> es un <c>ObjectId</c>; si no, 422 sin repetir el valor.</summary>
internal sealed class ServiceRouteValidator : AbstractValidator<ServiceRoute>
{
    public ServiceRouteValidator() => RuleFor(x => x.Id).Must(ServiceRules.ValidObjectId).WithMessage(ServiceRules.Invalid);
}

/// <summary>Ruta de SRV-01 (R-02): <c>id</c> es un <c>ObjectId</c>; si no, 422 sin repetir el valor.</summary>
internal sealed class TechnicianServicesRouteValidator : AbstractValidator<TechnicianServicesRoute>
{
    public TechnicianServicesRouteValidator() => RuleFor(x => x.Id).Must(ServiceRules.ValidObjectId).WithMessage(ServiceRules.Invalid);
}
