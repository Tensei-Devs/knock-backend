using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Modules.Tecnicos.Dtos;

/// <summary>Consulta de TEC-14 (spec 046, R-37): <c>filtro</c> es la palabra de todos los oficios, auto, agua o luz (si falta, sin filtrar) y <c>colonia</c> es opcional (24 hexadecimales del catálogo).</summary>
internal sealed class ExploreQuery
{
    public const string AllFilter = "todo";

    [FromQuery(Name = "filtro")] public string? Filtro { get; set; }

    [FromQuery(Name = "colonia")] public string? Colonia { get; set; }
}

/// <summary>Oficio con cuántos técnicos visibles lo ofrecen.</summary>
internal sealed record ExploreService(string Clave, string Nombre, int Tecnicos);

/// <summary>Oficio sugerido que aún no está entre los populares.</summary>
internal sealed record ExploreIdea(string Clave, string Nombre);

/// <summary>
/// Respuesta de TEC-14 (R-49): cada bloque trae como máximo 10 elementos. Sin sesión solo se llenan los bloques públicos (populares, mejor calificados e ideas);
/// <c>trabajosReales</c> y <c>cercaDeTi</c> llegan nulos.
/// </summary>
internal sealed record ExploreResponse(
    string Filtro,
    IReadOnlyList<ExploreService> ServiciosPopulares,
    IReadOnlyList<TechnicianProfileResponse> MejorCalificados,
    IReadOnlyList<ExploreIdea> Ideas,
    IReadOnlyList<WorkPhotoResponse>? TrabajosReales,
    IReadOnlyList<TechnicianProfileResponse>? CercaDeTi);

internal enum ExploreStatus
{
    Ok,
    ColoniaNotFound,
}

internal sealed record ExploreOutcome(ExploreStatus Status, ExploreResponse? Response);
