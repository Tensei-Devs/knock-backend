using Knock.Market.Api.Common.Errors;

namespace Knock.Market.Api.Modules.Requests.Domain;

/// <summary>Valores de configuración del tope de una cotización (spec 020, R-14). Los de visita salen de configuración, no del código de las pantallas.</summary>
internal sealed record QuotePricingOptions
{
    /// <summary>Sección de configuración que enlaza con este tipo.</summary>
    public const string SectionName = "Requests:Pricing";

    public int VisitFeeMin { get; init; } = 100;

    public int VisitFeeMax { get; init; } = 250;

    /// <summary>Margen del precio fijo, en porcentaje.</summary>
    public int FixedMarginPercent { get; init; } = 10;

    /// <summary>Margen sobre el máximo del rango para electricidad, en porcentaje.</summary>
    public int ElectricityRangeMarginPercent { get; init; } = 20;

    /// <summary>Margen sobre el máximo del rango para plomería y mecánica, en porcentaje.</summary>
    public int OtherRangeMarginPercent { get; init; } = 25;

    /// <summary>Diferencia mínima, en pesos, entre el tope y el precio base para exigir aprobación del cliente.</summary>
    public int ApprovalThreshold { get; init; } = 100;
}

/// <summary>Tope calculado de una cotización y si el excedente exige aprobación.</summary>
internal readonly record struct QuoteCap(int Tope, bool RequiereAprobacion);

/// <summary>Cálculo del tope y validación de montos de una cotización (spec 020, R-13 y R-14). Montos en pesos MXN enteros.</summary>
internal static class QuotePricing
{
    public const int MinAmount = 1;
    public const int MaxAmount = 200_000;

    public static QuoteCap Calculate(PriceType type, string oficioClave, int? precio, int? precioMin, int? precioMax, QuotePricingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        switch (type)
        {
            case PriceType.Fijo:
                {
                    var price = RequireAmount(precio);
                    return Build(price, WithMargin(price, options.FixedMarginPercent), options);
                }

            case PriceType.Rango:
                {
                    var min = RequireAmount(precioMin);
                    var max = RequireAmount(precioMax);
                    if (min >= max)
                    {
                        throw Invalid("The range minimum must be lower than the maximum.");
                    }

                    return Build(max, WithMargin(max, RangeMargin(oficioClave, options)), options);
                }

            case PriceType.Visita:
                {
                    var fee = RequireAmount(precio);
                    if (fee < options.VisitFeeMin || fee > options.VisitFeeMax)
                    {
                        throw Invalid("The visit fee is outside the configured bounds.");
                    }

                    return new QuoteCap(fee, false);
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown price type.");
        }
    }

    private static QuoteCap Build(int basePrice, int cap, QuotePricingOptions options) =>
        new(cap, cap - basePrice >= options.ApprovalThreshold);

    private static int RangeMargin(string oficioClave, QuotePricingOptions options) => oficioClave switch
    {
        "ele" => options.ElectricityRangeMarginPercent,
        "plo" or "mec" => options.OtherRangeMarginPercent,
        _ => throw Invalid("Unknown trade key."),
    };

    /// <summary>Tope en pesos enteros, redondeado hacia arriba (el técnico nunca pierde por redondeo).</summary>
    private static int WithMargin(int amount, int percent) => (int)(((long)amount * (100 + percent) + 99) / 100);

    private static int RequireAmount(int? amount)
    {
        if (amount is null)
        {
            throw Invalid("A required amount is missing.");
        }

        if (amount < MinAmount || amount > MaxAmount)
        {
            throw Invalid("The amount must be a whole number of pesos between 1 and 200000.");
        }

        return amount.Value;
    }

    private static DomainException Invalid(string message) => new(DomainErrorKind.Unprocessable, message);
}
