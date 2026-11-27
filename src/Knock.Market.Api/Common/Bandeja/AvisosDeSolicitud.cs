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
}

/// <summary>Una persona a la que avisar y el recurso del aviso; si falta alguno, ese aviso no se puede armar.</summary>
internal sealed record DestinoDeAviso(string? Destinatario, string? Recurso);

/// <summary>Cómo un evento del buzón de Mercado se convierte en un aviso del catálogo cerrado de la spec 037 (§4): tipo, rol y de dónde salen la persona y el recurso.</summary>
internal sealed record AvisoDeSolicitud(
    string EventType,
    string NoticeType,
    string Role,
    Func<IDestinatariosDeAviso, BsonDocument, CancellationToken, Task<DestinoDeAviso[]>> ResolveAsync);

/// <summary>
/// Los eventos de Mercado que son avisos de la spec 037 (R-11 y §4): quién lo recibe sale de las specs 020 (CA-06), 021 (R-18), 028 (R-07 y §2.2) y 029 (R-07 y R-09). El texto es el fijo del catálogo de
/// Mensajería; viajan solo identificadores. El aviso de cotización vencida a los técnicos no está aquí: las specs del servidor no dicen qué evento lo produce.
/// </summary>
internal static class AvisosDeSolicitud
{
    private const string _client = "cliente";
    private const string _technician = EntregaOpinionModerada.TechnicianRole;
    private const string _requestField = "solicitudId";

    public static IReadOnlyList<AvisoDeSolicitud> All { get; } =
    [
        new("SolicitudEnviadaATecnico", "solicitud_nueva", _technician, InvitedAsync),
        new("SolicitudSinTecnicos", "solicitud_sin_tecnicos", _client, OwnerAsync),
        new("CotizacionEnviada", "cotizacion_nueva", _client, OwnerAsync),
        new("CotizacionAceptada", "cotizacion_aceptada", _technician, ChosenAsync),
        new("SolicitudCancelada", "solicitud_cancelada", _technician, QuotersAsync),
        new("SolicitudVencida", "solicitud_vencida", _client, ExpiredAsync),
    ];

    private static async Task<DestinoDeAviso[]> InvitedAsync(IDestinatariosDeAviso people, BsonDocument data, CancellationToken cancellationToken)
    {
        var request = Text(data, _requestField);
        var technician = Text(data, "tecnicoId");
        var person = request is null || technician is null ? null : await people.InvitedTechnicianAsync(request, technician, cancellationToken);
        return [new(person, request)];
    }

    private static async Task<DestinoDeAviso[]> OwnerAsync(IDestinatariosDeAviso people, BsonDocument data, CancellationToken cancellationToken)
    {
        var request = Text(data, _requestField);
        var person = request is null ? null : await people.ClientAsync(request, cancellationToken);
        return [new(person, request)];
    }

    private static async Task<DestinoDeAviso[]> ChosenAsync(IDestinatariosDeAviso people, BsonDocument data, CancellationToken cancellationToken)
    {
        var request = Text(data, _requestField);
        var quote = Text(data, "cotizacionId");
        var visit = request is null || quote is null ? null : await people.VisitAsync(request, quote, cancellationToken);
        return [new(Text(data, "tecnicoUsuarioId"), visit)];
    }

    /// <summary>Solo los técnicos con cotización enviada al cancelar (spec 029, R-07); sin ninguno no hay a quién avisar.</summary>
    private static async Task<DestinoDeAviso[]> QuotersAsync(IDestinatariosDeAviso people, BsonDocument data, CancellationToken cancellationToken)
    {
        var request = Text(data, _requestField);
        if (request is null || !data.TryGetValue("tecnicosAvisar", out var list) || !list.IsBsonArray)
        {
            return [new(null, request)];
        }

        var targets = new List<DestinoDeAviso>();
        foreach (var technician in list.AsBsonArray)
        {
            var person = technician.IsString ? await people.QuotingTechnicianAsync(request, technician.AsString, cancellationToken) : null;
            targets.Add(new(person, request));
        }

        return [.. targets];
    }

    private static Task<DestinoDeAviso[]> ExpiredAsync(IDestinatariosDeAviso people, BsonDocument data, CancellationToken cancellationToken) =>
        Task.FromResult<DestinoDeAviso[]>([new(Text(data, "clienteId"), Text(data, _requestField))]);

    private static string? Text(BsonDocument data, string field) => data.TryGetValue(field, out var value) && value.IsString ? value.AsString : null;
}
