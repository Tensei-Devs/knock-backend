using System.ComponentModel;

namespace Knock.Market.Api.Modules.Requests.Dtos;

/// <summary>Técnico que cotizó, tal como lo ve el cliente al comparar (spec 021). Sin teléfono: el contacto solo llega con la visita.</summary>
internal sealed record QuoteTechnician(string Id, string Nombre, string? FotoUrl, double? Calificacion, bool Verificado, string NivelVerificacion);

/// <summary>
/// Cotización dentro de la comparación (spec 021, §5). <c>Cubre</c> es el texto corto de lo que cubre según su tipo de precio y <c>Etiquetas</c> son marcas
/// calculadas por el servidor, por ejemplo la más barata o la más pronto.
/// </summary>
internal sealed record QuoteComparisonItem(
    string Id,
    string Estado,
    string TipoPrecio,
    int Precio,
    int? PrecioMin,
    int? PrecioMax,
    int Tope,
    bool RequiereAprobacion,
    string Dia,
    string Hora,
    string? Nota,
    string Cubre,
    IReadOnlyList<string> Etiquetas,
    QuoteTechnician Tecnico);

/// <summary>
/// Respuesta de <c>GET /solicitudes/{id}/cotizaciones</c>: hasta 5 cotizaciones vivas y si el cliente ya puede elegir. <c>TiposMixtos</c> es verdadero cuando
/// hay precios fijos y rangos o visitas juntos (no son comparables uno a uno); <c>Motivo</c> explica por qué <c>PuedeElegir</c> es falso; <c>VisitaId</c>
/// aparece si la solicitud ya tiene cita confirmada.
/// </summary>
internal sealed record QuoteComparison(
    string SolicitudId,
    IReadOnlyList<QuoteComparisonItem> Cotizaciones,
    int TotalCotizaciones,
    bool TiposMixtos,
    bool PuedeElegir,
    string? Motivo,
    string? VisitaId);

/// <summary>Consulta de la comparación (spec 021): <c>orden</c> es <c>recomendadas</c>, <c>precio</c>, <c>calificacion</c> o <c>pronto</c>.</summary>
internal sealed class QuoteComparisonQuery
{
    [Description("Sort order: recomendadas (default), precio, calificacion or pronto.")]
    public string? Orden { get; set; }
}

/// <summary>Cuerpo de <c>POST /solicitudes/{id}/elegir</c>.</summary>
/// <param name="CotizacionId">Identificador (24 hexadecimales) de la cotización que se elige.</param>
internal sealed record ChooseQuoteBody(string? CotizacionId);

/// <summary>Técnico elegido, con el contacto público que se revela solo después de elegir.</summary>
internal sealed record ChosenTechnician(string Id, string Nombre, string? FotoUrl, bool Verificado, string NivelVerificacion, string? Telefono, string? Whatsapp);

/// <summary>Lugar de la cita: tipo (<c>domicilio</c> o <c>taller</c>) y dirección.</summary>
internal sealed record AppointmentPlace(string Tipo, string Direccion);

/// <summary>Cita confirmada al elegir una cotización (spec 021, §5).</summary>
internal sealed record Appointment(
    string VisitaId,
    string SolicitudId,
    string CotizacionId,
    string Estado,
    string Modalidad,
    string Dia,
    string Hora,
    int Precio,
    int Tope,
    ChosenTechnician Tecnico,
    AppointmentPlace Lugar);
