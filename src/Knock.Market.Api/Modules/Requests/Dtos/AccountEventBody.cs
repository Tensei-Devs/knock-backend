namespace Knock.Market.Api.Modules.Requests.Dtos;

/// <summary>
/// Cuerpo de <c>POST /api/v1/interno/eventos</c> (spec 054, R-15; spec 059, §5): <c>CuentaSuspendida</c> o <c>CuentaReactivada</c> de Identidad. Solo identificadores; los campos que
/// Mercado no usa (<c>reporteId</c>, <c>desde</c>) se aceptan y se ignoran.
/// </summary>
internal sealed class AccountEventBody
{
    public string? EventoId { get; set; }

    public string? Tipo { get; set; }

    public string? UsuarioId { get; set; }

    public string? ReporteId { get; set; }

    public string? Desde { get; set; }
}
