using Knock.Market.Api.Modules.Requests.Data;
using Knock.Market.Api.Modules.Requests.Dtos;
using MongoDB.Bson;

namespace Knock.Market.Api.Modules.Requests.Services;

/// <summary>Lectura del reporte de «no terminado» para la ruta interna que usa Identidad (spec 053): <see langword="null"/> si la visita no existe, no tiene reporte o el identificador no es válido.</summary>
internal sealed class NotFinishedReportReadService(VisitCloseStore store)
{
    public async Task<NotFinishedReportResponse?> FindAsync(string visitId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(visitId, out var id) || await store.FindNotFinishedReportAsync(id, cancellationToken) is not { } report)
        {
            return null;
        }

        return new NotFinishedReportResponse(report.VisitId, report.ClientId, report.TechnicianUserId, report.Text, report.CreatedAtUtc);
    }
}
