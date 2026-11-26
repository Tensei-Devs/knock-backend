using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Safety.Data;
using Knock.Identity.Api.Modules.Safety.Domain;
using Knock.Identity.Api.Modules.Safety.Dtos;

namespace Knock.Identity.Api.Modules.Safety.Services;

/// <summary>
/// Consumidor de <c>ReporteNoTerminadoCreado</c> (spec 053; spec 050, R-29 y R-32): abre el reporte en la cola de moderación a partir de la visita. El evento solo trae identificadores; el texto se lee
/// por la ruta interna de Mercado, así que la bandeja nunca lleva datos personales. Es idempotente por visita: repetir el evento no abre otro reporte. Si Mercado no responde lanza 503 para que quien
/// entrega reintente; si ya no hay reporte (404) no hay nada que abrir. Las personas deben coincidir con las de la respuesta de Mercado y no se registran con el texto (spec 043, R-22).
/// </summary>
internal sealed class NotFinishedReportConsumer(ReportesStore store, IMarketNotFinishedReports market)
{
    public const string EventType = "ReporteNoTerminadoCreado";
    public const string UnavailableCode = "market_report_unavailable";

    /// <summary>Abre el reporte si no existe; <see langword="true"/> si lo abrió ahora, <see langword="false"/> si ya existía o Mercado ya no tiene el reporte.</summary>
    public async Task<bool> ConsumeAsync(NotFinishedReportEventBody evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        var result = await market.ReadAsync(evento.VisitaId!, cancellationToken);
        if (result.Status == MarketReportStatus.NotFound)
        {
            return false;
        }

        if (result is not { Status: MarketReportStatus.Found, Report: { } report }
            || !string.Equals(report.VisitaId, evento.VisitaId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(report.ClienteId, evento.ClienteId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(report.TecnicoUsuarioId, evento.TecnicoUsuarioId, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(DomainErrorKind.ServiceUnavailable, "Market did not return the report.", UnavailableCode);
        }

        var created = new DateTime(report.CreadoEn.Ticks - (report.CreadoEn.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc);
        return await store.OpenFromVisitAsync(new NewReport(report.ClienteId, report.TecnicoUsuarioId, ReportReferenceKinds.Visit, report.VisitaId, ReportRules.NotFinishedReason, report.Texto, [], created), cancellationToken);
    }
}
