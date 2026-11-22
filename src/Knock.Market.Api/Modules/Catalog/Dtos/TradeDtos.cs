using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Modules.Catalog.Dtos;

/// <summary>Ruta con la clave de un oficio (spec 045, OFI-02): un objeto tipado con validador, nunca un <c>string</c> suelto.</summary>
internal sealed class TradeKeyRoute
{
    [FromRoute(Name = "clave")]
    public string? Clave { get; init; }
}

/// <summary>Detalle público de un oficio activo (spec 045, OFI-02): lo que la app necesita para pedir cotización. Sin parámetros de precio, <c>activo</c> ni identificadores internos.</summary>
/// <param name="Clave">Clave estable del catálogo.</param>
/// <param name="Nombre">Nombre para mostrar.</param>
/// <param name="Modalidad"><c>domicilio</c>, <c>taller</c> o <c>ambos</c>: decide si la app pide dirección y código de visita.</param>
/// <param name="FrasesSugeridas">Frases de ejemplo para describir el problema; vacía si el oficio no tiene.</param>
internal sealed record TradeDetailResponse(string Clave, string Nombre, string Modalidad, IReadOnlyList<string> FrasesSugeridas);

/// <summary>Un oficio tal como lo ve el panel (spec 045, OFI-03 a OFI-05): con <c>activo</c> y los parámetros de precio. Un parámetro es <c>null</c> si el oficio se sembró antes de que existieran.</summary>
internal sealed record AdminTradeResponse(
    string Clave,
    string Nombre,
    string Modalidad,
    IReadOnlyList<string> FrasesSugeridas,
    decimal? TopePorcentaje,
    decimal? TopeFijoPorcentaje,
    int? PisoAprobacion,
    int? VisitaMaxima,
    bool Activo);

/// <summary>Todos los oficios, incluidos los inactivos (OFI-03). El catálogo es acotado, así que no se pagina (decisión D9 de la spec TT-PRU-01).</summary>
internal sealed record AdminTradeListResponse(IReadOnlyList<AdminTradeResponse> Items);

/// <summary>Límites de los cuerpos de los oficios (spec 009): 4 KiB bastan para el nombre, las frases y los parámetros; más responde 413.</summary>
internal static class TradeBodies
{
    public const int MaxBodyBytes = 4096;
}

/// <summary>Cuerpo de <c>POST /api/v1/admin/oficios</c> (OFI-04): la clave y los parámetros del oficio nuevo. Todos son obligatorios salvo las frases y <c>activo</c> (verdadero por omisión, R-18).</summary>
internal sealed record CreateTradeBody(
    string? Clave,
    string? Nombre,
    string? Modalidad,
    IReadOnlyList<string>? FrasesSugeridas,
    decimal? TopePorcentaje,
    decimal? TopeFijoPorcentaje,
    int? PisoAprobacion,
    int? VisitaMaxima,
    bool? Activo);

/// <summary>
/// Cuerpo de <c>PUT /api/v1/admin/oficios/{clave}</c> (OFI-05): edición parcial, solo cambian los campos enviados. <c>Clave</c> existe para rechazarla: la clave no se puede cambiar (D-04), así que
/// solo se acepta si es igual a la de la ruta.
/// </summary>
internal sealed record UpdateTradeBody(
    string? Clave,
    string? Nombre,
    string? Modalidad,
    IReadOnlyList<string>? FrasesSugeridas,
    decimal? TopePorcentaje,
    decimal? TopeFijoPorcentaje,
    int? PisoAprobacion,
    int? VisitaMaxima,
    bool? Activo)
{
    /// <summary>Nombres (camelCase) de los campos que la edición cambia.</summary>
    public IReadOnlyList<string> ChangedFields => [.. ChangedFieldNames()];

    private IEnumerable<string> ChangedFieldNames()
    {
        if (Nombre is not null) { yield return "nombre"; }
        if (Modalidad is not null) { yield return "modalidad"; }
        if (FrasesSugeridas is not null) { yield return "frasesSugeridas"; }
        if (TopePorcentaje is not null) { yield return "topePorcentaje"; }
        if (TopeFijoPorcentaje is not null) { yield return "topeFijoPorcentaje"; }
        if (PisoAprobacion is not null) { yield return "pisoAprobacion"; }
        if (VisitaMaxima is not null) { yield return "visitaMaxima"; }
        if (Activo is not null) { yield return "activo"; }
    }
}
