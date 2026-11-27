using MongoDB.Bson;

namespace Knock.Market.Api.Common.Bandeja;

/// <summary>Lo que los avisos de solicitudes leen de Mercado para saber a qué persona avisar. Los eventos llevan identificadores; aquí se resuelven al entregar, sin cambiar su forma.</summary>
internal interface IDestinatariosDeAviso
{
    /// <summary>La persona dueña de la solicitud, o <see langword="null"/>.</summary>
    Task<string?> ClientAsync(string requestId, CancellationToken cancellationToken);

    /// <summary>La persona del técnico que recibió la solicitud (<c>envios</c>), o <see langword="null"/>.</summary>
    Task<string?> InvitedTechnicianAsync(string requestId, string technicianId, CancellationToken cancellationToken);

    /// <summary>La persona del técnico que cotizó la solicitud (<c>cotizaciones</c>), o <see langword="null"/>.</summary>
    Task<string?> QuotingTechnicianAsync(string requestId, string technicianId, CancellationToken cancellationToken);

    /// <summary>La visita creada al elegir la cotización, o <see langword="null"/>.</summary>
    Task<string?> VisitAsync(string requestId, string quoteId, CancellationToken cancellationToken);

    /// <summary>La persona técnica de la visita y el rol de quien la canceló (<c>cancelacion.por</c>), o <see langword="null"/> si no existe.</summary>
    Task<PersonasDeVisita?> VisitPeopleAsync(string visitId, CancellationToken cancellationToken);
}

/// <summary>Lo que un aviso de visita lee de la visita al entregar; <see cref="CancelledBy"/> es el rol de quien canceló.</summary>
internal sealed record PersonasDeVisita(string? TechnicianUserId, string? CancelledBy);

/// <summary>Un aviso del catálogo (<see cref="Aviso"/>) para una persona con su rol y su recurso; si falta la persona, el rol o el recurso, ese aviso no se puede armar.</summary>
internal sealed record DestinoDeAviso(string Aviso, string? Rol, string? Destinatario, string? Recurso);

/// <summary>Cómo un evento del buzón de Mercado se convierte en avisos del catálogo cerrado de la spec 037 (§4): cuáles, a quién, con qué rol y de dónde salen la persona y el recurso.</summary>
internal sealed record AvisoDeEvento(
    string EventType,
    Func<IDestinatariosDeAviso, BsonDocument, CancellationToken, Task<DestinoDeAviso[]>> ResolveAsync);

/// <summary>
/// Los eventos de solicitudes que son avisos (spec 037, enmienda del 2026-11-27): quién recibe cada uno. El texto es el fijo del catálogo de Mensajería; viajan solo identificadores. <c>SolicitudVencida</c>
/// produce dos avisos distintos: <c>solicitud_vencida</c> a la persona dueña y <c>cotizacion_vencida</c> a cada técnico de <c>tecnicosAvisar</c>.
/// </summary>
internal static class AvisosDeSolicitud
{
    private const string _requestField = "solicitudId";

    public static IReadOnlyList<AvisoDeEvento> All { get; } =
    [
        new("SolicitudEnviadaATecnico", InvitedAsync),
        new("SolicitudSinTecnicos", (people, data, cancellationToken) => OwnerAsync("solicitud_sin_tecnicos", people, data, cancellationToken)),
        new("CotizacionEnviada", (people, data, cancellationToken) => OwnerAsync("cotizacion_nueva", people, data, cancellationToken)),
        new("CotizacionAceptada", ChosenAsync),
        new("SolicitudCancelada", (people, data, cancellationToken) => QuotersAsync("solicitud_cancelada", people, data, cancellationToken)),
        new("SolicitudVencida", ExpiredAsync),
    ];

    private static async Task<DestinoDeAviso[]> InvitedAsync(IDestinatariosDeAviso people, BsonDocument data, CancellationToken cancellationToken)
    {
        var request = DatosDeAviso.Text(data, _requestField);
        var technician = DatosDeAviso.Text(data, "tecnicoId");
        var person = request is null || technician is null ? null : await people.InvitedTechnicianAsync(request, technician, cancellationToken);
        return [new("solicitud_nueva", DatosDeAviso.Technician, person, request)];
    }

    private static async Task<DestinoDeAviso[]> OwnerAsync(string notice, IDestinatariosDeAviso people, BsonDocument data, CancellationToken cancellationToken)
    {
        var request = DatosDeAviso.Text(data, _requestField);
        var person = request is null ? null : await people.ClientAsync(request, cancellationToken);
        return [new(notice, DatosDeAviso.Client, person, request)];
    }

    private static async Task<DestinoDeAviso[]> ChosenAsync(IDestinatariosDeAviso people, BsonDocument data, CancellationToken cancellationToken)
    {
        var request = DatosDeAviso.Text(data, _requestField);
        var quote = DatosDeAviso.Text(data, "cotizacionId");
        var visit = request is null || quote is null ? null : await people.VisitAsync(request, quote, cancellationToken);
        return [new("cotizacion_aceptada", DatosDeAviso.Technician, DatosDeAviso.Text(data, "tecnicoUsuarioId"), visit)];
    }

    /// <summary>Los técnicos de <c>tecnicosAvisar</c>, los que tenían una cotización enviada (spec 029, R-07 y R-09); con la lista vacía no hay a quién avisar y sin la lista el aviso no se puede armar.</summary>
    private static async Task<DestinoDeAviso[]> QuotersAsync(string notice, IDestinatariosDeAviso people, BsonDocument data, CancellationToken cancellationToken)
    {
        var request = DatosDeAviso.Text(data, _requestField);
        if (request is null || !data.TryGetValue("tecnicosAvisar", out var list) || !list.IsBsonArray)
        {
            return [new(notice, DatosDeAviso.Technician, null, request)];
        }

        var targets = new List<DestinoDeAviso>();
        foreach (var technician in list.AsBsonArray)
        {
            var person = technician.IsString ? await people.QuotingTechnicianAsync(request, technician.AsString, cancellationToken) : null;
            targets.Add(new(notice, DatosDeAviso.Technician, person, request));
        }

        return [.. targets];
    }

    private static async Task<DestinoDeAviso[]> ExpiredAsync(IDestinatariosDeAviso people, BsonDocument data, CancellationToken cancellationToken) =>
    [
        new("solicitud_vencida", DatosDeAviso.Client, DatosDeAviso.Text(data, "clienteId"), DatosDeAviso.Text(data, _requestField)),
        .. await QuotersAsync("cotizacion_vencida", people, data, cancellationToken),
    ];
}
