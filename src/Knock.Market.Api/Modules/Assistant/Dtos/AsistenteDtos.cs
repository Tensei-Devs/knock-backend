using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Modules.Assistant.Dtos;

/// <summary>Cuerpo de <c>POST /asistente/conversaciones</c> (spec 052, ASI-01): un turno con el asistente Knock.</summary>
internal sealed record TurnoBody(string? Mensaje, string? Foto, string? Contexto)
{
    public const int MaxBodyBytes = 8 * 1024;
    public const int MaxMessage = 1000;
    public const int MaxContext = 200;
}

/// <summary>Cuerpo de <c>POST /asistente/resumen-chat</c> y <c>/borrador-cotizacion</c> (ASI-02 y ASI-03). El identificador de la persona nunca viene en el cuerpo: sale del <c>sub</c>.</summary>
internal sealed record ConversacionBody(string? ConversacionId)
{
    public const int MaxBodyBytes = 1024;
}

/// <summary>Consulta de <c>GET /asistente/rango-precio</c> (ASI-04).</summary>
internal sealed class RangoPrecioQuery
{
    [FromQuery(Name = "oficio")]
    public string? Oficio { get; set; }

    [FromQuery(Name = "tipoTrabajo")]
    public string? TipoTrabajo { get; set; }
}

/// <summary>Cuerpo de <c>PUT /asistente/bitacora/{solicitudId}</c> (ASI-05): listas de hasta 10 elementos de hasta 200 caracteres.</summary>
internal sealed record BitacoraBody(IReadOnlyList<string>? Sabe, IReadOnlyList<string>? PorConfirmar, IReadOnlyList<string>? QueLlevar)
{
    public const int MaxBodyBytes = 8 * 1024;
    public const int MaxItems = 10;
    public const int MaxItemLength = 200;
}

/// <summary>Identificador de solicitud en la ruta: 24 hexadecimales; si no, 422 <c>errors.solicitudId</c>.</summary>
internal sealed class SolicitudRoute
{
    [FromRoute(Name = "solicitudId")]
    public string? SolicitudId { get; set; }
}

/// <summary>Toda respuesta generada es una sugerencia que la persona revisa (spec 052, R-22): <c>generadoPorIa</c> siempre es verdadero y <c>origen</c> dice si salió del modelo o de la plantilla.</summary>
internal sealed record TurnoResponse(string Categoria, string Mensaje, bool PideFoto, string? Seguridad, string Origen, bool GeneradoPorIa);

internal sealed record ResumenChatResponse(IReadOnlyList<string> Sabe, IReadOnlyList<string> PorConfirmar, IReadOnlyList<string> QueLlevar, string Origen, bool GeneradoPorIa);

internal sealed record BorradorCotizacionResponse(decimal PrecioSugerido, string TiempoEstimado, string Dia, string Nota, string Explicacion, string Origen, bool GeneradoPorIa);

/// <summary>Rango sugerido por la referencia de mercado de la spec 048 (percentiles 25 a 75 de cotizaciones aceptadas, con el rango de la categoría como respaldo).</summary>
internal sealed record RangoPrecioResponse(decimal Minimo, decimal Maximo, int Muestras, string Fuente);

internal sealed record BitacoraResponse(string SolicitudId, IReadOnlyList<string> Sabe, IReadOnlyList<string> PorConfirmar, IReadOnlyList<string> QueLlevar, DateTime ActualizadaEn);
