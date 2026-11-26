namespace Knock.Market.Api.Modules.Requests.Domain;

/// <summary>Plazos de la solicitud (spec 029, R-08 y R-10): horas hasta el vencimiento y minutos entre barridos de respaldo.</summary>
internal sealed class SolicitudesOptions
{
    public const string SectionName = "Solicitudes";
    public const int MaxHours = 168;
    public const int SweepBatch = 100;

    /// <summary>Horas de vida de una solicitud sin elegir (1 a 168, 48 por omisión). Es la misma que usa la spec 020 para <c>venceEn</c>.</summary>
    public int HorasVencimiento { get; set; } = RequestRules.LifetimeHours;

    /// <summary>Minutos entre dos barridos de <c>BarrerVencidas</c> (10 por omisión).</summary>
    public int MinutosBarrido { get; set; } = 10;

    public bool IsValid() => HorasVencimiento is >= 1 and <= MaxHours && MinutosBarrido is >= 1 and <= 1440;
}
