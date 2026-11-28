using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Messaging.Api.Modules.Conversaciones.Dtos;

/// <summary>Cuerpo de <c>POST /conversaciones</c> (spec 030, R-04): <c>tecnicoId</c> es obligatorio para el cliente y lo omite el técnico. Los campos son opcionales para que el validador responda 422 y no el 400 del binding.</summary>
internal sealed record CrearConversacionBody(string? SolicitudId, string? TecnicoId);

/// <summary>Cuerpo de <c>POST /conversaciones/{id}/leido</c> (spec 030, R-19): la fecha UTC del último mensaje que la app mostró.</summary>
internal sealed record LeidoBody(DateTimeOffset? Hasta);

/// <summary>La otra parte: nombre público y foto, nunca teléfono, correo, <c>sub</c> ni dirección (R-06). <c>fotoUrl</c> viaja siempre, en <c>null</c> cuando no hay.</summary>
internal sealed record Contraparte(string Nombre, string? FotoUrl);

/// <summary>Cuerpo <c>Conversacion</c> de la spec 030 (§5), visto por quien pide.</summary>
internal sealed record ConversacionView(
    string ConversacionId,
    string SolicitudId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? VisitaId,
    string Rol,
    Contraparte Contraparte,
    string Servicio,
    string Estado,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MotivoCierre,
    bool ContactoVisible,
    string AvisoSeguridad,
    int NoLeidos);

/// <summary>Resultado de crear: la conversación y si se creó (201) o ya existía (200).</summary>
internal sealed record ConversacionCreada(ConversacionView Conversacion, bool Creada);

/// <summary>Último mensaje de una conversación en la lista: el texto ya ocultado y recortado a 80 caracteres (R-14).</summary>
internal sealed record UltimoMensajeView(string Texto, DateTime EnviadoEn, string AutorRol);

/// <summary>Un elemento de <c>GET /conversaciones</c> (R-17). <c>ultimoMensaje</c> viaja en <c>null</c> mientras no haya mensajes.</summary>
internal sealed record ConversacionItem(
    string ConversacionId,
    string SolicitudId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? VisitaId,
    string Rol,
    Contraparte Contraparte,
    string Servicio,
    string Estado,
    UltimoMensajeView? UltimoMensaje,
    int NoLeidos);

/// <summary>Página de <c>GET /conversaciones</c>: sin conversaciones, <c>items: []</c> y <c>totalNoLeidos: 0</c>.</summary>
internal sealed record ConversacionPage(IReadOnlyList<ConversacionItem> Items, int TotalNoLeidos, string? NextCursor);

/// <summary>Cuerpo de <c>GET /conversaciones/no-leidas</c> (R-18).</summary>
internal sealed record NoLeidasView(int Total);

/// <summary>Un mensaje del historial (R-16): sin <c>sub</c>, nombre ni teléfono de quien lo escribió.</summary>
internal sealed record MensajeView(string MensajeId, string AutorRol, string Texto, bool ContactoOculto, DateTime EnviadoEn);

/// <summary>Parámetros de <c>GET /conversaciones</c>: <c>limit</c> de 1 a 20 (20 por omisión) y el cursor de la página anterior.</summary>
internal sealed class ConversacionesQuery
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 20;

    [FromQuery(Name = "cursor")]
    [Description("Opaque cursor returned as nextCursor by the previous page.")]
    public string? Cursor { get; set; }

    [FromQuery(Name = "limit")]
    [Description("Page size, 1 to 20. Defaults to 20.")]
    public int? Limit { get; set; }
}

/// <summary>Parámetros de <c>GET /conversaciones/{id}/mensajes</c>: <c>limit</c> de 1 a 50 (30 por omisión) y el cursor de la página anterior.</summary>
internal sealed class MensajesQuery
{
    public const int DefaultLimit = 30;
    public const int MaxLimit = 50;

    [FromQuery(Name = "cursor")]
    [Description("Opaque cursor returned as nextCursor by the previous page.")]
    public string? Cursor { get; set; }

    [FromQuery(Name = "limit")]
    [Description("Page size, 1 to 50. Defaults to 30.")]
    public int? Limit { get; set; }
}
