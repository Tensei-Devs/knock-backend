using MongoDB.Bson;

namespace Knock.Market.Api.Common.Bandeja;

/// <summary>
/// Los eventos de la visita que son avisos (spec 054, enmienda del 2026-11-27), con la visita como recurso. Las personas salen de los <c>datos</c> del evento; quién propuso viaja como <c>propuestaPor</c> y
/// quién canceló se lee de la visita. Sin ese rol el aviso no se puede armar y el mensaje queda <c>fallido</c>. <c>VisitaCerrada</c> no avisa.
/// </summary>
internal static class AvisosDeVisita
{
    private const string _proposedByField = "propuestaPor";

    public static IReadOnlyList<AvisoDeEvento> All { get; } =
    [
        new("ReprogramacionPropuesta", (_, data, _) => Ready(ToOtherParty("reprogramacion_propuesta", data, DatosDeAviso.Text(data, _proposedByField)))),
        new("VisitaReprogramada", (_, data, _) => Ready(
            DatosDeAviso.ToRole("visita_reprogramada", data, DatosDeAviso.Client),
            DatosDeAviso.ToRole("visita_reprogramada", data, DatosDeAviso.Technician))),
        new("ReprogramacionRechazada", (_, data, _) => Ready(ToProposer(data))),
        new("ReprogramacionVencida", (_, data, _) => Ready(ToProposer(data))),
        new("CancelacionTardia", CancelledAsync),
        new("ClienteAusente", (_, data, _) => Ready(DatosDeAviso.ToRole("cliente_ausente", data, DatosDeAviso.Client))),
        new("VisitaTerminada", (_, data, _) => Ready(DatosDeAviso.ToRole("visita_terminada", data, DatosDeAviso.Client))),
    ];

    private static Task<DestinoDeAviso[]> Ready(params DestinoDeAviso[] targets) => Task.FromResult(targets);

    private static DestinoDeAviso ToProposer(BsonDocument data) =>
        DatosDeAviso.ToRole("reprogramacion_sin_cambio", data, DatosDeAviso.Text(data, _proposedByField));

    private static DestinoDeAviso ToOtherParty(string notice, BsonDocument data, string? role) => DatosDeAviso.ToRole(notice, data, role switch
    {
        DatosDeAviso.Client => DatosDeAviso.Technician,
        DatosDeAviso.Technician => DatosDeAviso.Client,
        _ => null,
    });

    private static async Task<DestinoDeAviso[]> CancelledAsync(IDestinatariosDeAviso people, BsonDocument data, CancellationToken cancellationToken)
    {
        var visit = DatosDeAviso.Text(data, DatosDeAviso.VisitField);
        var found = visit is null ? null : await people.VisitPeopleAsync(visit, cancellationToken);
        return [ToOtherParty("visita_cancelada", data, found?.CancelledBy)];
    }
}
