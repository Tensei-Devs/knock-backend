using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Identity.Api.Modules.Moderation.Dtos;

/// <summary>Ruta con el identificador del caso (<c>ObjectId</c>) de las rutas de detalle y de decisión (spec 053, R-05).</summary>
internal sealed class ReviewRoute
{
    [FromRoute(Name = "id")]
    public string? Id { get; set; }
}

/// <summary>Filtros de fecha y paginación comunes de las colas del panel (spec 053).</summary>
internal abstract class QueueQueryBase
{
    [FromQuery(Name = "desde")]
    public DateTimeOffset? Desde { get; set; }

    [FromQuery(Name = "hasta")]
    public DateTimeOffset? Hasta { get; set; }

    [FromQuery(Name = "cursor")]
    [Description("Opaque cursor returned as nextCursor by the previous page.")]
    public string? Cursor { get; set; }

    [FromQuery(Name = "limite")]
    [Description("Page size, 1 to 50. Defaults to 20.")]
    public int Limite { get; set; } = 20;
}

/// <summary>Parámetros de <c>GET /admin/verificaciones</c> (spec 053, R-02 y R-16). Las fechas son UTC ISO 8601.</summary>
internal sealed class VerificationQueueQuery : QueueQueryBase
{
    [FromQuery(Name = "tipo")]
    [Description("identidad (default) or recuperacion.")]
    public string? Tipo { get; set; }

    [FromQuery(Name = "estado")]
    [Description("en_revision by default.")]
    public string? Estado { get; set; }

}

/// <summary>Una fila de la cola: solo identificador, tipo, estado y fecha; nunca fotos ni datos personales (R-16).</summary>
internal sealed record VerificationQueueItem(string Id, string Tipo, string Nivel, string Estado, DateTime EnviadaEn);

internal sealed record VerificationQueuePage(
    IReadOnlyList<VerificationQueueItem> Items,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? NextCursor);

/// <summary>Enlace de 5 minutos a una foto (R-17). Nunca se guarda, se cachea ni se registra.</summary>
internal sealed record VerificationPhotoLink(string Tipo, string Url, DateTime VenceEn);

internal sealed record VerificationDetail(string Id, string Tipo, string Nivel, string Estado, DateTime EnviadaEn, IReadOnlyList<VerificationPhotoLink> Fotos);

/// <summary>Cuerpo de <c>POST /admin/verificaciones/{id}/decision</c> (R-08 y R-18). Ningún campo acepta el identificador de otra persona (R-23).</summary>
internal sealed class VerificationDecisionBody
{
    public const int MaxBodyBytes = 2048;

    public string? Decision { get; set; }

    public string? Motivo { get; set; }
}

internal sealed record VerificationDecisionResult(string Id, string Estado, DateTime DecididaEn);

/// <summary>Parámetros de <c>GET /admin/reportes</c> (spec 053, R-11 y R-19).</summary>
internal sealed class ReportQueueQuery : QueueQueryBase
{
    [FromQuery(Name = "gravedad")]
    public string? Gravedad { get; set; }

    [FromQuery(Name = "estado")]
    [Description("abierto and en_apelacion by default.")]
    public string? Estado { get; set; }

}

/// <summary>Una fila de la cola de reportes: motivo y texto, nunca teléfono ni dirección (R-19).</summary>
internal sealed record ReportQueueItem(
    string Id,
    string Motivo,
    string Descripcion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Gravedad,
    string Estado,
    DateTime CreadoEn);

internal sealed record ReportQueuePage(
    IReadOnlyList<ReportQueueItem> Items,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? NextCursor);

/// <summary>Cuerpo de <c>POST /admin/reportes/{id}/decision</c> (R-14 y R-20).</summary>
internal sealed class ReportDecisionBody
{
    public const int MaxBodyBytes = 2048;

    public string? Gravedad { get; set; }

    public string? Motivo { get; set; }
}

internal sealed record ReportDecisionResult(string Id, string Estado, string Gravedad, string Respuesta);
