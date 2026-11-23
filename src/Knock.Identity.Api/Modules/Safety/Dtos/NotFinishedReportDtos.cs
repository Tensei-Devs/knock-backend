using System.Text.Json;
using System.Text.Json.Serialization;

namespace Knock.Identity.Api.Modules.Safety.Dtos;

/// <summary>
/// Cuerpo de <c>POST /api/v1/interno/reportes-no-terminados</c> (spec 053): <c>ReporteNoTerminadoCreado</c> que Mercado entrega desde su bandeja de salida (spec 050, R-32). Solo identificadores; el texto no
/// viaja aquí, Identidad lo lee por la ruta interna de Mercado. Cualquier otro campo queda en <see cref="Extra"/> y el validador lo rechaza con 422.
/// </summary>
internal sealed record NotFinishedReportEventBody(string? EventoId, string? Tipo, string? VisitaId, string? ClienteId, string? TecnicoUsuarioId)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}

/// <summary>El reporte de «no terminado» tal como lo devuelve Mercado: identificadores, texto y fecha. El texto nunca se registra.</summary>
internal sealed record MarketNotFinishedReport(string VisitaId, string ClienteId, string TecnicoUsuarioId, string Texto, DateTime CreadoEn)
{
    public sealed override string ToString() => nameof(MarketNotFinishedReport);
}
