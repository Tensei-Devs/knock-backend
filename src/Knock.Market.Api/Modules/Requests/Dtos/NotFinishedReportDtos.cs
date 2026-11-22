namespace Knock.Market.Api.Modules.Requests.Dtos;

/// <summary>
/// Respuesta de <c>GET /api/v1/interno/visitas/{visitaId}/reporte-no-terminado</c> (spec 053): lo que Identidad necesita para abrir el reporte en la cola del panel. El texto lo escribió el
/// cliente en el reporte de «no terminado» (spec 050, R-29); nunca lleva teléfono ni dirección.
/// </summary>
internal sealed record NotFinishedReportResponse(string VisitaId, string ClienteId, string TecnicoUsuarioId, string Texto, DateTime CreadoEn);
