using Microsoft.AspNetCore.Mvc;

namespace Knock.Messaging.Api.Modules.Notices.Dtos;

/// <summary>Consulta de <c>GET /api/v1/avisos</c> (AVI-01). Cada valor llega como texto para que un valor inválido responda 422 y no un 400 del enlace de datos.</summary>
internal sealed class ListNoticesQuery
{
    [FromQuery(Name = "cursor")]
    public string? Cursor { get; init; }

    [FromQuery(Name = "limite")]
    public string? Limite { get; init; }

    [FromQuery(Name = "leido")]
    public string? Leido { get; init; }

    [FromQuery(Name = "comoRol")]
    public string? ComoRol { get; init; }
}

/// <summary>Ruta de <c>POST /api/v1/avisos/{id}/leido</c> (AVI-03).</summary>
internal sealed class MarkNoticeReadRequest
{
    [FromRoute(Name = "id")]
    public string? Id { get; init; }
}

/// <summary>Un aviso tal como lo ve la persona (R-10): sin códigos de visita, direcciones ni datos sensibles.</summary>
internal sealed record NoticeResponse(string Id, string Tipo, string ComoRol, string Titulo, string Texto, object? Destino, bool Leido, DateTime CreadoEn);

internal sealed record NoticeListResponse(IReadOnlyList<NoticeResponse> Items, string? SiguienteCursor);

/// <summary>Contador de no leídos, hasta 99; <c>MasDe99</c> avisa a la app que debe mostrar «99+».</summary>
internal sealed record UnreadCountResponse(int Cantidad, bool MasDe99);

internal sealed record NoticePreferenceResponse(string Tipo, bool Activo, bool Apagable);

internal sealed record NoticePreferencesResponse(IReadOnlyList<NoticePreferenceResponse> Tipos);
