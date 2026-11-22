using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace Knock.Market.Api.Modules.Tecnicos.Dtos;

/// <summary>Consulta de TEC-04 (spec 046, R-12): la zona visible del mapa, el oficio y el zoom (1 a 20; 12 si falta). Ningún valor se guarda ni se registra (R-40).</summary>
internal sealed class MapQuery
{
    public const int DefaultZoom = 12;

    [FromQuery(Name = "norte")] public string? Norte { get; set; }

    [FromQuery(Name = "sur")] public string? Sur { get; set; }

    [FromQuery(Name = "este")] public string? Este { get; set; }

    [FromQuery(Name = "oeste")] public string? Oeste { get; set; }

    [FromQuery(Name = "oficio")] public string? Oficio { get; set; }

    [FromQuery(Name = "zoom")] public string? Zoom { get; set; }
}

/// <summary>Un grupo del mapa: el centro de su celda (nunca la posición de un técnico), cuántos técnicos hay y, si es uno solo, su identificador.</summary>
internal sealed record MapGroup(double Latitud, double Longitud, int Cantidad, string? TecnicoId);

/// <summary>Respuesta de TEC-04: hasta 200 grupos, los más poblados primero.</summary>
internal sealed record MapResponse(IReadOnlyList<MapGroup> Grupos);

/// <summary>Posición aproximada de un técnico visible, solo para agrupar; no sale de la capa de servicio.</summary>
internal sealed record MapPoint(ObjectId Id, double Latitude, double Longitude);

internal enum MapStatus
{
    Ok,
    OficioInvalid,
}

internal sealed record MapOutcome(MapStatus Status, MapResponse? Response);
