using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Knock.Market.Api.Modules.Requests.Dtos;

/// <summary>Consulta de <c>GET /agenda</c> (spec 050, R-24): días <c>yyyy-MM-dd</c> de la Ciudad de México; por omisión hoy y los 14 días siguientes.</summary>
internal sealed class AgendaQuery
{
    [Description("First day of the range, yyyy-MM-dd. Defaults to today.")]
    public string? Desde { get; set; }

    [Description("Last day of the range, yyyy-MM-dd. Defaults to 14 days after the first day. The range is 31 days at most.")]
    public string? Hasta { get; set; }

    [Description("Opaque cursor returned as nextCursor by the previous page.")]
    public string? Cursor { get; set; }

    [Description("Page size, 1 to 50. Defaults to 20.")]
    public int Limite { get; set; } = 20;
}

/// <summary>Cuerpo de <c>POST /visitas/{id}/reprogramar</c>: la fecha y hora propuestas. No acepta identificadores de personas (R-31).</summary>
internal sealed record RescheduleBody(DateTimeOffset? FechaHora)
{
    /// <summary>Tamaño máximo del cuerpo de las rutas de la spec 050: 8 KB (spec 009).</summary>
    public const int MaxBodyBytes = 8192;
}

/// <summary>Cuerpo de <c>POST /visitas/{id}/reprogramar/respuesta</c>: <c>true</c> acepta la propuesta y <c>false</c> la rechaza.</summary>
internal sealed record RescheduleReplyBody(bool? Acepta);

/// <summary>Cuerpo de <c>POST /visitas/{id}/cancelar</c>: motivo obligatorio de 8 a 300 caracteres.</summary>
internal sealed record CancelVisitBody(string? Motivo);

/// <summary>Una visita de la agenda del técnico: solo lo que necesita para planear el día, sin código, dirección, nombre ni teléfono del cliente.</summary>
internal sealed record AgendaItem(string Id, string Estado, string Modalidad, DateTimeOffset FechaHora, string Dia, string Hora, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Precio);

internal sealed record AgendaResponse(IReadOnlyList<AgendaItem> Items, string? NextCursor);

/// <summary>Propuesta de reprogramación registrada (200).</summary>
internal sealed record RescheduleProposalResponse(string VisitaId, DateTimeOffset FechaHora, string PropuestaPor, DateTimeOffset VenceEn);

/// <summary>Resultado de responder la propuesta: la cita vigente tras la respuesta.</summary>
internal sealed record RescheduleReplyResponse(string VisitaId, bool Aceptada, DateTimeOffset FechaHora, int Reprogramaciones);

/// <summary>Visita cancelada (200): <c>tardia</c> es verdadero si faltaban menos de 24 horas.</summary>
internal sealed record CancelVisitResponse(string VisitaId, string Estado, bool Tardia);

/// <summary>Ausencia del cliente registrada (200): la visita queda <c>no_presentado</c> y no hay penalización.</summary>
internal sealed record ClientAbsentResponse(string VisitaId, string Estado, string QuienFalto);

/// <summary>Cuerpo de <c>POST /visitas/{id}/terminar</c> (spec 050, R-28): qué hizo, recomendación opcional y de 1 a 5 fotos privadas del técnico.</summary>
internal sealed record FinishVisitBody(string? Descripcion, string? Recomendacion, IReadOnlyList<string>? Fotos);

/// <summary>Visita terminada (200). No hay confirmación del cliente.</summary>
internal sealed record FinishVisitResponse(string VisitaId, string Estado, DateTimeOffset TerminadaEn);

/// <summary>Cuerpo de <c>POST /visitas/{id}/no-terminado</c> (spec 050, R-29): texto de 8 a 500 caracteres.</summary>
internal sealed record NotFinishedBody(string? Texto);

/// <summary>Reporte de no terminado creado (201): queda en revisión.</summary>
internal sealed record NotFinishedResponse(string VisitaId, string Estado);

/// <summary>Cuerpo de <c>PUT /visitas/{id}/permiso-fotos</c> (spec 050, R-30): ausente se toma como que no.</summary>
internal sealed record PhotoPermissionBody(bool? Permiso);

/// <summary>Permiso de publicar las fotos del trabajo tras el cambio (200).</summary>
internal sealed record PhotoPermissionResponse(string VisitaId, bool Permiso);
