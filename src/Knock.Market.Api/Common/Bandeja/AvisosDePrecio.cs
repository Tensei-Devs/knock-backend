using MongoDB.Bson;

namespace Knock.Market.Api.Common.Bandeja;

/// <summary>
/// Los eventos de cotización final y de ajuste de precio que son avisos (spec 049, enmienda del 2026-11-27), con la visita como recurso. El destinatario sale de los <c>datos</c>; <c>AjustePrecioDecidido</c> y
/// <c>AjustePrecioVencido</c> no lo llevan, así que el técnico se lee de la visita al entregar. Nunca viajan precios, motivos ni descripciones.
/// </summary>
internal static class AvisosDePrecio
{
    private const string _finalQuoteAnswered = "cotizacion_final_respondida";

    public static IReadOnlyList<AvisoDeEvento> All { get; } =
    [
        new("CotizacionFinalEnviada", (_, data, _) => Ready("cotizacion_final_nueva", data, DatosDeAviso.Client)),
        new("CotizacionFinalDecidida", (_, data, _) => Ready(_finalQuoteAnswered, data, DatosDeAviso.Technician)),
        new("CotizacionFinalVencida", (_, data, _) => Ready(_finalQuoteAnswered, data, DatosDeAviso.Technician)),
        new("AjustePrecioPropuesto", (_, data, _) => Ready("ajuste_precio_nuevo", data, DatosDeAviso.Client)),
        new("AjustePrecioDecidido", AdjustmentAnsweredAsync),
        new("AjustePrecioVencido", AdjustmentAnsweredAsync),
    ];

    private static Task<DestinoDeAviso[]> Ready(string notice, BsonDocument data, string role) =>
        Task.FromResult<DestinoDeAviso[]>([DatosDeAviso.ToRole(notice, data, role)]);

    private static async Task<DestinoDeAviso[]> AdjustmentAnsweredAsync(IDestinatariosDeAviso people, BsonDocument data, CancellationToken cancellationToken)
    {
        var visit = DatosDeAviso.Text(data, DatosDeAviso.VisitField);
        var found = visit is null ? null : await people.VisitPeopleAsync(visit, cancellationToken);
        return [new("ajuste_precio_respondido", DatosDeAviso.Technician, found?.TechnicianUserId, visit)];
    }
}
