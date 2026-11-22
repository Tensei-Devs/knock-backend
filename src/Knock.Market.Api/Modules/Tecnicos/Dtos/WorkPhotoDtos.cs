using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Modules.Tecnicos.Dtos;

/// <summary>Consulta de TEC-07 (spec 046, R-25): cursor y límite de la página; 20 por defecto y 50 como máximo.</summary>
internal sealed class WorkPhotoQuery
{
    public const int DefaultLimit = 20;

    public const int MaxLimit = 50;

    [FromQuery(Name = "cursor")] public string? Cursor { get; set; }

    [FromQuery(Name = "limite")] public string? Limite { get; set; }
}

/// <summary>Una foto de un trabajo terminado, con permiso del cliente y moderada: solo su dirección pública y la fecha (R-44).</summary>
internal sealed record WorkPhotoResponse(string Id, string Url, DateTime CreadoEn);

/// <summary>Respuesta de TEC-07: fotos de la página, más recientes primero, y el cursor de la siguiente (nula si no hay más).</summary>
internal sealed record WorkPhotoListResponse(IReadOnlyList<WorkPhotoResponse> Fotos, string? NextCursor);
