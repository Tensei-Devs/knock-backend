using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Modules.Reviews.Dtos;

/// <summary>Partes de la calificación (R-13): cada una de 1 a 5 y todas opcionales.</summary>
internal sealed record ReviewParts(int? Puntualidad, int? Calidad, int? PrecioJusto, int? Trato);

/// <summary>Cuerpo de <c>POST /visitas/{id}/opinion</c> (OPI-02, R-07): ningún identificador de persona se acepta (R-18).</summary>
/// <param name="Fotos">De 0 a 3 identificadores de archivo (<c>archivoId</c>, 32 hexadecimales), sin repetidos, moderadas; más de 3 responde 422 <c>errors.fotos</c>.</param>
/// <param name="Calificacion">Entero de 1 a 5.</param>
/// <param name="Partes">Cada parte de 1 a 5.</param>
/// <param name="Comentario">Hasta 500 caracteres.</param>
/// <param name="GuardarTecnicoConfianza">Opcional: suma al técnico a los de confianza.</param>
internal sealed record CreateReviewBody(int? Calificacion, ReviewParts? Partes, string? Comentario, IReadOnlyList<string>? Fotos, bool? GuardarTecnicoConfianza)
{
    public const int MaxBodyBytes = 4 * 1024;
}

/// <summary>Cuerpo de <c>POST /opiniones/{id}/respuesta</c> (OPI-03, R-10).</summary>
internal sealed record AnswerReviewBody(string? Texto)
{
    public const int MaxBodyBytes = 2 * 1024;
}

/// <summary>Cuerpo de <c>POST /admin/opiniones/{id}/ocultar</c> (spec 061, R-01): solo el motivo; quien oculta sale del token del panel.</summary>
internal sealed record HideReviewBody(string? Motivo)
{
    public const int MaxBodyBytes = 2 * 1024;
}

/// <summary>Respuesta del técnico a una opinión (R-15).</summary>
internal sealed record ReviewAnswer(string Texto, DateTime Fecha);

/// <summary>Una opinión publicada. <c>Anonimizada</c> es verdadero si la cuenta del cliente se eliminó: no lleva nombre ni foto (R-16). Siempre sale como servicio verificado (R-12).</summary>
internal sealed record ReviewItem(
    string Id,
    int Calificacion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReviewParts? Partes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Comentario,
    IReadOnlyList<string> Fotos,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReviewAnswer? Respuesta,
    bool Anonimizada,
    bool ServicioVerificado,
    DateTime CreadoEn);

/// <summary>Distribución por estrellas (<c>"1"</c> a <c>"5"</c>) con el promedio de las opiniones que cuentan (R-14).</summary>
internal sealed record ReviewSummary(double? Promedio, int Total, IReadOnlyDictionary<string, int> Distribucion);

/// <summary>Página de opiniones de un técnico, de la más nueva a la más vieja (OPI-01, R-16).</summary>
internal sealed record ReviewPage(IReadOnlyList<ReviewItem> Items, ReviewSummary Resumen, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? NextCursor);

/// <summary>Respuesta de <c>POST /visitas/{id}/opinion</c>: la opinión creada y el estado de su comentario (<c>publicada</c>, <c>pendiente</c> o <c>rechazada</c>).</summary>
internal sealed record CreatedReview(string Id, int Calificacion, string Moderacion, DateTime CreadoEn);

/// <summary>El técnico de la ruta de <c>GET /tecnicos/{id}/opiniones</c>, con cursor y límite.</summary>
internal sealed class TechnicianReviewsQuery
{
    [FromRoute(Name = "id")]
    public string? Id { get; set; }

    [FromQuery(Name = "cursor")]
    public string? Cursor { get; set; }

    [FromQuery(Name = "limite")]
    public int Limite { get; set; } = 20;
}

/// <summary>El identificador de la ruta de <c>POST /visitas/{id}/opinion</c>.</summary>
internal sealed class ReviewVisitRoute
{
    [FromRoute(Name = "id")]
    public string? Id { get; set; }
}

/// <summary>El identificador de la ruta de <c>POST /opiniones/{id}/respuesta</c>.</summary>
internal sealed class ReviewRoute
{
    [FromRoute(Name = "id")]
    public string? Id { get; set; }
}
